using Microsoft.CodeAnalysis;

using System.Reflection;

namespace FUICompiler
{
    internal static class TypeSymbolExtensions
    {
        static Queue<ITypeSymbol> cache = new Queue<ITypeSymbol>();

        /// <summary>
        /// 判断一个类型是否继承自某个类型或者实现了某个接口
        /// </summary>
        /// <param name="symbol">源类型</param>
        /// <param name="type">目标类型</param>
        /// <returns></returns>
        internal static bool Extends(this ITypeSymbol symbol, Type type)
        {
            if (symbol == null || type == null)
            {
                return false;
            }

            var openList = cache;
            openList.Clear();
            openList.Enqueue(symbol);

            while(openList.Count > 0)
            {
                var current = openList.Dequeue();

                if (current.Matches(type))
                {
                    return true;
                }

                if (current.BaseType != null)
                {
                    openList.Enqueue(current.BaseType);
                }

                foreach (var @interface in current.Interfaces)
                {
                    openList.Enqueue(@interface);
                }
            }

            return false;
        }

        /// <summary>
        /// 判断一个类型是否和另一个类型匹配 
        /// </summary>
        /// <param name="symbol">源类型</param>
        /// <param name="type">目标类型</param>
        /// <returns></returns>
        internal static bool Matches(this ITypeSymbol symbol, Type type)
        {
            switch (symbol.SpecialType)
            {
                case SpecialType.System_Void:
                    return type == typeof(void);
                case SpecialType.System_Boolean:
                    return type == typeof(bool);
                case SpecialType.System_Int32:
                    return type == typeof(int);
                case SpecialType.System_Single:
                    return type == typeof(float);
            }

            if (type.IsArray)
            {
                return symbol is IArrayTypeSymbol array && Matches(array.ElementType, type.GetElementType());
            }

            if (!(symbol is INamedTypeSymbol named))
            {
                return false;
            }

            if (type.IsConstructedGenericType)
            {
                var args = type.GetTypeInfo().GenericTypeArguments;
                if (args.Length != named.TypeArguments.Length)
                {
                    return false;
                }

                for (var i = 0; i < args.Length; i++)
                {
                    if (!Matches(named.TypeArguments[i], args[i]))
                    {
                        return false;
                    }
                }
                    
                return Matches(named.ConstructedFrom, type.GetGenericTypeDefinition());
            }

            return named.MetadataName == type.Name && named.ContainingNamespace?.ToDisplayString() == type.Namespace;
        }

        /// <summary>
        /// 判断某个类型是否是指定类型
        /// </summary>
        /// <param name="symbol">要判断的类型</param>
        /// <param name="type">目标类型</param>
        /// <returns></returns>
        internal static bool IsType(this ITypeSymbol symbol, Type type)
        {
            return Matches(symbol, type);
        }

        internal static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                yield return current;
                current = current.BaseType;
            }
        }

        internal static bool InheritsFromOrEquals(this ITypeSymbol type, ITypeSymbol baseType)
        {
            foreach (var t in type.GetBaseTypesAndThis())
            {
                if (SymbolEqualityComparer.Default.Equals(t, baseType))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool InheritsFrom(this ITypeSymbol type, Type baseType)
        {
            foreach (var t in type.GetBaseTypesAndThis())
            {
                if (t.IsType(baseType))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsGenericTypeFrom(this ITypeSymbol type, Type baseType)
        {
            if (!baseType.IsGenericType)
            {
                return false;
            }

            foreach (var t in type.GetBaseTypesAndThis())
            {
                if (t is not INamedTypeSymbol named)
                {
                    continue;
                }

                if (named.IsGenericType 
                    && named.TypeArguments.Count() == baseType.GetGenericArguments().Length 
                    && named.ConstructedFrom.Matches(baseType.GetGenericTypeDefinition().BaseType))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
