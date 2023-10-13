using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

namespace UnityExt.Core {

    /// <summary>
    /// Miscelaneous extensions to different types.
    /// </summary>
    public static class Extensions {

        #region Enum <-> Numbers Lambdas
        internal class EnumNumberConvert<T,U> {
            internal static System.Func<T,U> CompileCastMethod() {
                var inputs = Expression.Parameter(typeof(T));
                var body   = Expression.Convert(inputs,typeof(U)); // Cast Y -> X;
                var lambda = Expression.Lambda<Func<T,U>>(body,inputs);
                Func<T,U> method = lambda.Compile();
                return method;
            }
            internal static Func<T,U> Convert   = CompileCastMethod();            
        }

        public static byte   ToByte   <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,byte  >.Convert(v); }
        public static sbyte  ToSByte  <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,sbyte >.Convert(v); }
        public static ushort ToUShort <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,ushort>.Convert(v); }
        public static short  ToShort  <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,short >.Convert(v); }
        public static uint   ToUInt   <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,uint  >.Convert(v); }
        public static int    ToInt    <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,int   >.Convert(v); }
        public static ulong  ToULong  <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,ulong >.Convert(v); }
        public static long   ToLong   <T>(this T v) where T : System.Enum { return EnumNumberConvert<T,long  >.Convert(v); }

        public static T ToEnum <T>(this byte   v) where T : System.Enum { return EnumNumberConvert<byte  ,T>.Convert(v); }
        public static T ToEnum <T>(this sbyte  v) where T : System.Enum { return EnumNumberConvert<sbyte ,T>.Convert(v); }
        public static T ToEnum <T>(this ushort v) where T : System.Enum { return EnumNumberConvert<ushort,T>.Convert(v); }
        public static T ToEnum <T>(this short  v) where T : System.Enum { return EnumNumberConvert<short ,T>.Convert(v); }
        public static T ToEnum <T>(this uint   v) where T : System.Enum { return EnumNumberConvert<uint  ,T>.Convert(v); }
        public static T ToEnum <T>(this int    v) where T : System.Enum { return EnumNumberConvert<int   ,T>.Convert(v); }
        public static T ToEnum <T>(this ulong  v) where T : System.Enum { return EnumNumberConvert<ulong ,T>.Convert(v); }
        public static T ToEnum <T>(this long   v) where T : System.Enum { return EnumNumberConvert<long  ,T>.Convert(v); }
        
        #endregion

    }


}