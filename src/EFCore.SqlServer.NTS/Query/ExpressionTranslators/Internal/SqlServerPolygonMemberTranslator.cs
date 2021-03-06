﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using GeoAPI.Geometries;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators.Internal
{
    /// <summary>
    ///     <para>
    ///         This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///         directly from your code. This API may change or be removed in future releases.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Singleton"/>. This means a single instance
    ///         is used by many <see cref="DbContext"/> instances. The implementation must be thread-safe.
    ///         This service cannot depend on services registered as <see cref="ServiceLifetime.Scoped"/>.
    ///     </para>
    /// </summary>
    public class SqlServerPolygonMemberTranslator : IMemberTranslator
    {
        private static readonly MemberInfo _exteriorRing = typeof(IPolygon).GetRuntimeProperty(nameof(IPolygon.ExteriorRing));
        private static readonly MemberInfo _numInteriorRings = typeof(IPolygon).GetRuntimeProperty(nameof(IPolygon.NumInteriorRings));

        private static readonly IDictionary<MemberInfo, string> _geometryMemberToFunctionName = new Dictionary<MemberInfo, string>
        {
            { _exteriorRing, "STExteriorRing" },
            { _numInteriorRings, "STNumInteriorRing" }
        };

        private readonly IRelationalTypeMappingSource _typeMappingSource;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public SqlServerPolygonMemberTranslator(IRelationalTypeMappingSource typeMappingSource)
            => _typeMappingSource = typeMappingSource;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual Expression Translate(MemberExpression memberExpression)
        {
            if (!typeof(IPolygon).IsAssignableFrom(memberExpression.Member.DeclaringType))
            {
                return null;
            }

            var storeType = memberExpression.FindSpatialStoreType();
            var isGeography = string.Equals(storeType, "geography", StringComparison.OrdinalIgnoreCase);

            var member = memberExpression.Member.OnInterface(typeof(IPolygon));
            if (isGeography)
            {
                if (Equals(_exteriorRing, member))
                {
                    return new SqlFunctionExpression(
                        memberExpression.Expression,
                        "RingN",
                        memberExpression.Type,
                        new[] { Expression.Constant(1) },
                        _typeMappingSource.FindMapping(typeof(ILineString), storeType));
                }

                if (Equals(_numInteriorRings, member))
                {
                    return Expression.Subtract(
                        new SqlFunctionExpression(
                            memberExpression.Expression,
                            "NumRings",
                            memberExpression.Type,
                            Enumerable.Empty<Expression>()),
                        Expression.Constant(1));
                }
            }
            else if (_geometryMemberToFunctionName.TryGetValue(member, out var functionName))
            {
                RelationalTypeMapping resultTypeMapping = null;
                if (typeof(IGeometry).IsAssignableFrom(memberExpression.Type))
                {
                    resultTypeMapping = _typeMappingSource.FindMapping(memberExpression.Type, storeType);
                }

                return new SqlFunctionExpression(
                    memberExpression.Expression,
                    functionName,
                    memberExpression.Type,
                    Enumerable.Empty<Expression>(),
                    resultTypeMapping);
            }

            return null;
        }
    }
}
