﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapper.Collection;
using AutoMapper.EquivalencyExpression;
using static System.Linq.Expressions.Expression;

namespace AutoMapper.Mappers
{
    public class EquivalentExpressionAddRemoveCollectionMapper : EnumerableMapperBase, IConfigurationObjectMapper
    {
        private readonly CollectionMapper _collectionMapper = new CollectionMapper();
        private readonly static EquivalentExpressionAddRemoveCollectionOptions _options = new EquivalentExpressionAddRemoveCollectionOptions();

        public IConfigurationProvider ConfigurationProvider { get; set; }

        public EquivalentExpressionAddRemoveCollectionMapper(Action<EquivalentExpressionAddRemoveCollectionOptions> configuration = default)
        {
            if(configuration != default)
            {
                configuration(_options);
            }
        }

        public static TDestination Map<TSource, TSourceItem, TDestination, TDestinationItem>(TSource source, TDestination destination, ResolutionContext context, IEquivalentComparer equivalentComparer)
            where TSource : IEnumerable<TSourceItem>
            where TDestination : ICollection<TDestinationItem>
        {
            if (source == null || destination == null)
            {
                return destination;
            }

            var destList = destination.ToLookup(x => equivalentComparer.GetHashCode(x)).ToDictionary(x => x.Key, x => x.ToList());

            var items = source.Select(x =>
            {
                var sourceHash = equivalentComparer.GetHashCode(x);

                var item = default(TDestinationItem);
                List<TDestinationItem> itemList;
                if (destList.TryGetValue(sourceHash, out itemList))
                {
                    item = itemList.FirstOrDefault(dest => equivalentComparer.IsEquivalent(x, dest));
                    if (item != null)
                    {
                        itemList.Remove(item);
                    }
                }
                return new { SourceItem = x, DestinationItem = item };
            });

            foreach (var keypair in items)
            {
                if (keypair.DestinationItem == null)
                {
                    var newItem = (TDestinationItem)context.Mapper.Map(keypair.SourceItem, null, typeof(TSourceItem), typeof(TDestinationItem));
                    destination.Add(newItem);
                    if (_options?.OnItemAdded != null)
                        _options.OnItemAdded(newItem);
                }
                else
                {
                    context.Mapper.Map(keypair.SourceItem, keypair.DestinationItem);
                }
            }

            foreach (var removedItem in destList.SelectMany(x => x.Value))
            {
                destination.Remove(removedItem);
                if (_options?.OnItemRemoved != null)
                    _options.OnItemRemoved(removedItem);
            }

            return destination;
        }

        private static readonly MethodInfo _mapMethodInfo = typeof(EquivalentExpressionAddRemoveCollectionMapper).GetRuntimeMethods().Single(x => x.IsStatic && x.Name == nameof(Map));
        private static readonly ConcurrentDictionary<TypePair, IObjectMapper> _objectMapperCache = new ConcurrentDictionary<TypePair, IObjectMapper>();

        public override bool IsMatch(TypePair typePair)
        {
            return typePair.SourceType.IsEnumerableType()
                   && typePair.DestinationType.IsCollectionType();
        }

        public override Expression MapExpression(IConfigurationProvider configurationProvider, ProfileMap profileMap, IMemberMap memberMap,
            Expression sourceExpression, Expression destExpression, Expression contextExpression)
        {
            var sourceType = TypeHelper.GetElementType(sourceExpression.Type);
            var destType = TypeHelper.GetElementType(destExpression.Type);

            var equivalencyExpression = this.GetEquivalentExpression(sourceType, destType);
            if (equivalencyExpression == null)
            {
                var typePair = new TypePair(sourceExpression.Type, destExpression.Type);
                return _objectMapperCache.GetOrAdd(typePair, _ =>
                {
                    var mappers = new List<IObjectMapper>(configurationProvider.GetMappers());
                    for (var i = mappers.IndexOf(this) + 1; i < mappers.Count; i++)
                    {
                        var mapper = mappers[i];
                        if (mapper.IsMatch(typePair))
                        {
                            return mapper;
                        }
                    }
                    return _collectionMapper;
                })
                .MapExpression(configurationProvider, profileMap, memberMap, sourceExpression, destExpression, contextExpression);
            }

            var method = _mapMethodInfo.MakeGenericMethod(sourceExpression.Type, sourceType, destExpression.Type, destType);
            var map = Call(null, method, sourceExpression, destExpression, contextExpression, Constant(equivalencyExpression));

            var notNull = NotEqual(destExpression, Constant(null));
            var collectionMapperExpression = _collectionMapper.MapExpression(configurationProvider, profileMap, memberMap, sourceExpression, destExpression, contextExpression);
            return Condition(notNull, map, Convert(collectionMapperExpression, destExpression.Type));
        }
    }

    public class EquivalentExpressionAddRemoveCollectionOptions
    {
        public Action<object> OnItemAdded { get; set; }
        public Action<object> OnItemRemoved { get; set; }
    }
}
