﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.Scaffolding.Internal
{
    public abstract class ModelCodeGeneratorTestBase
    {
        public static ConventionSet BuildNonValidatingConventionSet()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .AddDbContext<DbContext>(
                    (p, o) =>
                        o.UseSqlServer("Server=.")
                            .UseInternalServiceProvider(p))
                .BuildServiceProvider();

            using (var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                return serviceScope.ServiceProvider
                    .GetService<DbContext>()
                    .GetService<IConventionSetBuilder>()
                    .CreateConventionSet();
            }
        }

        protected void Test(
            Action<ModelBuilder> buildModel,
            ModelCodeGenerationOptions options,
            Action<ScaffoldedModel> assertScaffold,
            Action<IModel> assertModel)
        {
            var modelBuilder = new ModelBuilder(BuildNonValidatingConventionSet());
            modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion);
            buildModel(modelBuilder);
            var _ = modelBuilder.Model.Scaffolding().EntityTypeErrors;

            var model = modelBuilder.FinalizeModel();

            var services = new ServiceCollection()
                .AddEntityFrameworkDesignTimeServices();
            new SqlServerDesignTimeServices().ConfigureDesignTimeServices(services);

            var generator = services
                .BuildServiceProvider()
                .GetRequiredService<IModelCodeGenerator>();

            var scaffoldedModel = generator.GenerateModel(
                model,
                "TestNamespace",
                "TestNamespace",
                "TestNamespace",
                /*contextDir:*/ string.Empty,
                "TestDbContext",
                "Initial Catalog=TestDatabase",
                options);
            assertScaffold(scaffoldedModel);

            var build = new BuildSource
            {
                References =
                {
                    BuildReference.ByName("Microsoft.EntityFrameworkCore"),
                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational"),
                    BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer")
                },
                Sources = new List<string>(
                    new[] { scaffoldedModel.ContextFile.Code }.Concat(
                        scaffoldedModel.AdditionalFiles.Select(f => f.Code)))
            };

            var assembly = build.BuildInMemory();
            var context = (DbContext)assembly.CreateInstance("TestNamespace.TestDbContext");
            var compiledModel = context.Model;
            assertModel(compiledModel);
        }
    }
}
