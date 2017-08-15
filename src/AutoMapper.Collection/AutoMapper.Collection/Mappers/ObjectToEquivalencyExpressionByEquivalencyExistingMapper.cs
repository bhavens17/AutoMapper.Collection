using AutoMapper.EquivilencyExpression;
using AutoMapper.Impl;

namespace AutoMapper.Mappers
{
    public class ObjectToEquivalencyExpressionByEquivalencyExistingMapper : IObjectMapper
    {
        public object Map(ResolutionContext context, IMappingEngineRunner mapper)
        {
            var destExpressArgType = context.DestinationType.GetSinglePredicateExpressionArgumentType();
            var toSourceExpression = EquivilentExpressions.GetEquivilentExpression(context.SourceType, destExpressArgType) as IToSingleSourceEquivalentExpression;
            return toSourceExpression.ToSingleSourceExpression(context.SourceValue);
        }

        public bool IsMatch(ResolutionContext context)
        {
            var destExpressArgType = context.TypeMap.DestinationType.GetSinglePredicateExpressionArgumentType();
            if (destExpressArgType == null)
                return false;
            var expression = EquivilentExpressions.GetEquivilentExpression(context.TypeMap.SourceType, destExpressArgType);
            return expression is IToSingleSourceEquivalentExpression;
        }
    }
}