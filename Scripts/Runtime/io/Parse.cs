using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Math = System.Math;

namespace UnityExt.Core {

    /// <summary>
    /// Class that wraps numerical parsing from and to the general C# types, aiming for less to no GC and speed.
    /// </summary>
    public class Parse {

        #region struct UIntToFloat
        [StructLayout(LayoutKind.Explicit)]
        public struct UIntToFloat {
            [FieldOffset(0)] public uint  Bytes;
            [FieldOffset(0)] public float Value;
            [FieldOffset(0)] public byte  Byte0;
            [FieldOffset(1)] public byte  Byte1;
            [FieldOffset(2)] public byte  Byte2; 
            [FieldOffset(3)] public byte  Byte3;
        }
        #endregion

        #region struct ULongToDouble
        [StructLayout(LayoutKind.Explicit)]
        public struct ULongToDouble {
            [FieldOffset(0)] public ulong   Bytes;
            [FieldOffset(0)] public double  Value;
            [FieldOffset(0)] public byte    Byte0;
            [FieldOffset(1)] public byte    Byte1;
            [FieldOffset(2)] public byte    Byte2; 
            [FieldOffset(3)] public byte    Byte3;
            [FieldOffset(4)] public byte    Byte4;
            [FieldOffset(5)] public byte    Byte5;
            [FieldOffset(6)] public byte    Byte6;
            [FieldOffset(7)] public byte    Byte7;
        }
        #endregion

        /// <summary>
        /// Internal buffer to handle the operations.
        /// </summary>
        static private char            m_digits_sep  = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator[0];
        static private string          m_digits_lut  = "0123456789abcdef";
        static private char[]          m_buffer      = new char[128];
        static private StringBuilder[] m_sb_list     = new StringBuilder[32];        
        static private FieldInfo       m_sb_chars_fi = typeof(StringBuilder).GetField("m_ChunkChars"   , BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty);
        static private FieldInfo       m_sb_prev_fi  = typeof(StringBuilder).GetField("m_ChunkPrevious", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty);
        static private double[]        pow_cache;

        static Parse() {
            pow_cache = new double[64];
            for(int i=0;i<pow_cache.Length;i++) {
                pow_cache[i] = System.Math.Pow(10.0,(double)i);
            }
        }

        #region Auxiliary
        /// <summary>
        /// Hack the internals of a stringbuilder to fill a char array with its data
        /// </summary>        
        static private int PopulateBuffer(StringBuilder p_source) {
            if(p_source==null) return 0;
            //Navigate the StringBuilder graph and extract the chars
            char[] sb_chars;                                    
            //Traverse the graph for reverse iteration later
            StringBuilder sb_iterator = p_source;
            int sb_idx = 0;
            while(sb_iterator!=null) {
                if(sb_idx>=m_sb_list.Length)break;
                m_sb_list[sb_idx++]=sb_iterator;
                sb_iterator = (StringBuilder)m_sb_prev_fi.GetValue(sb_iterator);
            }
            int char_it=0;
            for(int i=sb_idx-1;i>=0;i--) {
                sb_iterator = m_sb_list[i];
                sb_chars    = (char[])m_sb_chars_fi.GetValue(sb_iterator);
                for(int j = 0; j < sb_chars.Length; j++) {
                    if(sb_chars[j]==0)break;
                    if(char_it>=m_buffer.Length) break;
                    m_buffer[char_it++] = sb_chars[j];
                }
            }
            return char_it;
        }
        static private int PopulateBuffer(string p_source) {
            int len = p_source.Length;
            len = Math.Min(len,m_buffer.Length);
            for(int i=0;i<len;i++)m_buffer[i]=p_source[i];
            return len;
        }

        /// <summary>
        /// Helper to write down ulongs
        /// </summary>        
        static private int NumberToChars(char[] p_buffer,ulong p_value,int p_offset) {
            ulong v = p_value;            
            int dc = DigitCount(p_value);
            for(int i=dc-1;i>=0;i--) {
                p_buffer[i+p_offset] = m_digits_lut[(int)(v%10)];                
                v/=10;
            }            
            return dc+p_offset;
        }

        /// <summary>
        /// Helper to write down decimal
        /// </summary>        
        static private int NumberToChars(char[] p_buffer,decimal p_value,int p_offset) {            
            decimal v = p_value;            
            int dc = DigitCount(p_value);
            for(int i=dc-1;i>=0;i--) {
                p_buffer[i+p_offset] = m_digits_lut[(int)(v%10)];                
                v/=10;
            }            
            return dc+p_offset;
        }

        /// <summary>
        /// Extracts the integer and fractional parts of a number as string.
        /// </summary>
        /// <param name="p_buffer">Char array to write</param>
        /// <param name="p_negative">Flag if negative number</param>
        /// <param name="p_int">Integer part</param>
        /// <param name="p_frac">Fractional part</param>
        static private void Extract(char[] p_buffer,int p_offset,int p_count,out bool p_negative,out ulong p_int,ulong p_base) {
            ulong vi = 0;            
            p_negative = false;                       
            ulong m = 1;
            for(int i=p_count-1;i>=0;i--) {
                char c = p_buffer[i+p_offset];
                if(c<=0) break;
                ulong vd=0;
                switch(c) {                                      
                    case '0': case '1': case '2': case '3': case '4': 
                    case '5': case '6': case '7': case '8': case '9':           vd = (ulong)( c-'0');     break;
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f': vd = (ulong)((c-'a')+10); break;
                }
                switch(c) {
                    case '-': p_negative = true;  break;                    
                    case '0': case '1': case '2': case '3': case '4': 
                    case '5': case '6': case '7': case '8': case '9': 
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f': {
                        ulong v  = vi;                        
                        v += vd * m;
                        m*=p_base;
                        vi=v;
                    }
                    break;
                }
            }
            p_int = vi;            
        }

        /// <summary>
        /// Extracts the integer and fractional parts of a number as string.
        /// </summary>
        /// <param name="p_buffer"></param>
        /// <param name="p_offset"></param>
        /// <param name="p_count"></param>
        /// <param name="p_negative"></param>
        /// <param name="p_int"></param>
        /// <param name="p_frac"></param>
        /// <param name="p_frac_lz"></param>
        static private void Extract(char[] p_buffer,int p_offset,int p_count,out bool p_negative,out double p_int,out double p_frac,out int p_frac_lz) {
            double vi = 0;
            double vf = 0;
            p_negative = false;
            bool is_vf = true;
            bool has_sep = false;
            int  sep_idx = -1;
            p_frac_lz  = 0;
            double m = 1;
            for(int i=p_count-1;i>=0;i--) {
                char c = p_buffer[i+p_offset];
                if(c<=0) break;
                switch(c) {
                    //found negative sign
                    case '-': p_negative = true;  break;
                    //found decimal separator
                    //store the index to check leading zero after sep
                    case '.': 
                    case ',': is_vf = false; m = 1; has_sep=true; sep_idx=i; break;
                    //increment digits (x10)
                    case '0': case '1': case '2': case '3': case '4': 
                    case '5': case '6': case '7': case '8': case '9': {
                        double v  = is_vf ? vf : vi;
                        double vd = (double)(c-'0');                        
                        v += vd * m;
                        m*=10.0;
                        if(is_vf) vf=v; else vi=v;
                    }
                    break;
                }
            }
            //accumulate leading zeros after separator
            int lz=0;
            if(has_sep) for(int i = sep_idx + 1; i < p_count; i++) { if(p_buffer[i] != '0') break; lz++; }
            //return it to decimal correc the fractional part
            p_frac_lz = lz;
            //if has separator assign integer and fractional parts
            if(has_sep) {
                p_int  = vi;
                p_frac = vf;
            }
            //if no separator fractional part is the integer (reverse digit iteration)
            else {
                p_int  = vf;
                p_frac = 0;
            }            
        }

        /// <summary>
        /// Extracts the integer and fractional parts of a number as string.
        /// </summary>
        /// <param name="p_buffer">Char array to write</param>
        /// <param name="p_negative">Flag if negative number</param>
        /// <param name="p_int">Integer part</param>
        /// <param name="p_frac">Fractional part</param>
        static private void Extract(char[] p_buffer,int p_offset,int p_count,out bool p_negative,out decimal p_int,decimal p_base) {
            decimal vi = 0;            
            p_negative = false;                       
            decimal m = 1;
            for(int i=p_count-1;i>=0;i--) {
                char c = p_buffer[i+p_offset];
                decimal vd=0;
                switch(c) {                                      
                    case '0': case '1': case '2': case '3': case '4': 
                    case '5': case '6': case '7': case '8': case '9':           vd = (decimal)( c-'0');     break;
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f': vd = (decimal)((c-'a')+10); break;
                }
                switch(c) {
                    case '-': p_negative = true;  break;                    
                    case '0': case '1': case '2': case '3': case '4': 
                    case '5': case '6': case '7': case '8': case '9': 
                    case 'a': case 'b': case 'c': case 'd': case 'e': case 'f': {
                        decimal v  = vi;                        
                        v += vd * m;
                        m*=p_base;
                        vi=v;
                    }
                    break;
                }
            }
            p_int = vi;
        }

        /// <summary>
        /// Finds the number of digits in a number
        /// https://stackoverflow.com/questions/4483886/how-can-i-get-a-count-of-the-total-number-of-digits-in-a-number/51099524#51099524
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        static public int DigitCount(uint n) {
            if (n < 0          ) return 1;
            if (n < 10L        ) return 1;
            if (n < 100L       ) return 2;
            if (n < 1000L      ) return 3;
            if (n < 10000L     ) return 4;
            if (n < 100000L    ) return 5;
            if (n < 1000000L   ) return 6;
            if (n < 10000000L  ) return 7;
            if (n < 100000000L ) return 8;
            if (n < 1000000000L) return 9;
            return 10;
        }
        static public int DigitCount(ulong n) {
            if (n < 0                     ) return 1;
            if (n < 10L                   ) return 1;
            if (n < 100L                  ) return 2;
            if (n < 1000L                 ) return 3;
            if (n < 10000L                ) return 4;
            if (n < 100000L               ) return 5;
            if (n < 1000000L              ) return 6;
            if (n < 10000000L             ) return 7;
            if (n < 100000000L            ) return 8;
            if (n < 1000000000L           ) return 9;
            if (n < 10000000000L          ) return 10;
            if (n < 100000000000L         ) return 11;
            if (n < 1000000000000L        ) return 12;
            if (n < 10000000000000L       ) return 13;
            if (n < 100000000000000L      ) return 14;
            if (n < 1000000000000000L     ) return 15;
            if (n < 10000000000000000L    ) return 16;
            if (n < 100000000000000000L   ) return 17;
            if (n < 1000000000000000000L  ) return 18;
            if (n < 10000000000000000000L ) return 19;            
            return 20;
        }
        static public int DigitCount(decimal n) {
            if (n < 0                                        ) return 1;
            if (n < 10M                                      ) return 1;
            if (n < 100M                                     ) return 2;
            if (n < 1000M                                    ) return 3;
            if (n < 10000M                                   ) return 4;
            if (n < 100000M                                  ) return 5;
            if (n < 1000000M                                 ) return 6;
            if (n < 10000000M                                ) return 7;
            if (n < 100000000M                               ) return 8;
            if (n < 1000000000M                              ) return 9;
            if (n < 10000000000M                             ) return 10;
            if (n < 100000000000M                            ) return 11;
            if (n < 1000000000000M                           ) return 12;
            if (n < 10000000000000M                          ) return 13;
            if (n < 100000000000000M                         ) return 14;
            if (n < 1000000000000000M                        ) return 15;
            if (n < 10000000000000000M                       ) return 16;
            if (n < 100000000000000000M                      ) return 17;
            if (n < 1000000000000000000M                     ) return 18;
            if (n < 100000000000000000000M                   ) return 19;            
            if (n < 1000000000000000000000M                  ) return 20;
            if (n < 10000000000000000000000M                 ) return 21;
            if (n < 100000000000000000000000M                ) return 22;
            if (n < 1000000000000000000000000M               ) return 23;
            if (n < 10000000000000000000000000M              ) return 24;
            if (n < 100000000000000000000000000M             ) return 25;
            if (n < 1000000000000000000000000000M            ) return 26;
            if (n < 10000000000000000000000000000M           ) return 27;            
            return 28;
        }

        /// <summary>
        /// Calculate the amount of bytes needed to write a positive number.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        static public int ByteCount(uint n) {
            if (n <           0) return 1;
            if (n < ((1<< 8)-1)) return 1;
            if (n < ((1<<16)-1)) return 2;
            if (n < ((1<<32)-1)) return 4;            
            return 4;
        }
        static public int ByteCount(ulong n) {
            if (n <           0) return 1;
            if (n < ((1<< 8)-1)) return 1;
            if (n < ((1<<16)-1)) return 2;
            if (n < ((1<<32)-1)) return 4;
            return 4;
        }

        #endregion

        #region Parsing

        #region bool
        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(char[] p_buffer,out bool p_value) { 
            p_value=false;
            switch(p_buffer[0]) {
                case 'T': case 't': case '1': p_value=true;  break;
                case 'F': case 'f': case '0': p_value=false; break;                
            }                        
        }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out bool p_value) { PopulateBuffer(p_buffer); To(m_buffer,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out bool p_value) { PopulateBuffer(p_buffer); To(m_buffer,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,bool p_value) { p_buffer[0] = p_value ? '1' : '0'; return 1; }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,bool p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }

        #endregion

        #region sbyte
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out sbyte p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = (sbyte)v; }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out sbyte p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out sbyte p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,sbyte p_value) { return From(p_buffer,(long)p_value); }
        
        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,sbyte p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region byte
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out byte p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = (byte)v; }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out byte p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out byte p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,byte p_value) { return From(p_buffer,(long)p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,byte p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region short
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out short p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = (short)v; }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out short p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out short p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,short p_value) { return From(p_buffer,(long)p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,short p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region ushort
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out ushort p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = (ushort)v; }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out ushort p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out ushort p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,ushort p_value) { return From(p_buffer,(long)p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,ushort p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region int
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out int p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = (int)v; }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out int p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out int p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,int p_value) { return From(p_buffer,(long)p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,int p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region uint
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out uint p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = (uint)v; }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out uint p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out uint p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,uint p_value) { return From(p_buffer,(long)p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,uint p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region ulong
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out ulong p_value) {                        
            bool nf = false;
            ulong vi;            
            Extract(p_buffer,p_offset,p_count,out nf,out vi,10);
            p_value=vi;
        }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out ulong p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out ulong p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,ulong p_value) { return NumberToChars(p_buffer,p_value,0); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,ulong p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region long
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out long p_value) {                        
            bool nf = false;
            ulong vi;            
            Extract(p_buffer,p_offset,p_count,out nf,out vi,10);
            p_value = nf ? -(long)vi : (long)vi;
        }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out long p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out long p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,long p_value) {            
            long v    = p_value;
            bool neg  = v<0;
            if(v<0) v=-v;                        
            if(neg)p_buffer[0]='-';
            int len = NumberToChars(p_buffer,(ulong)v,neg ? 1 : 0);            
            return len;
        }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,long p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region decimal
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out decimal p_value) {                        
            bool nf = false;
            decimal vi;
            Extract(p_buffer,p_offset,p_count,out nf,out vi,10);
            p_value = nf ? -vi : vi;
        }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out decimal p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out decimal p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,decimal p_value) {            
            decimal v    = p_value;
            bool    neg  = v<0;
            if(v<0) v=-v;                        
            if(neg)p_buffer[0]='-';
            int len = NumberToChars(p_buffer,v,neg ? 1 : 0);
            return len;
        }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,decimal p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region float
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out float p_value) {
            if(p_buffer[0]=='+')if(p_buffer[1]=='I') { p_value = float.PositiveInfinity; return; }
            if(p_buffer[0]=='-')if(p_buffer[1]=='I') { p_value = float.NegativeInfinity; return; }
            if(p_buffer[0]=='N')                     { p_value = float.NaN;              return; }            
            bool sn = false;
            for(int i = 0; i < p_buffer.Length; i++) {
                if(p_buffer[i]<=0) break;                
                if(p_buffer[i]=='e') { sn = true; break; }
                if(p_buffer[i]=='E') { sn = true; break; }
            }
            //Too lazy to parse scientific notation
            if(sn) { p_value = float.Parse(new string(p_buffer,0,p_count)); return; }
            double v;
            To(p_buffer,p_offset,p_count,out v);
            p_value=(float)v;
        }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out float p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out float p_value) { 
            int c=PopulateBuffer(p_buffer); 
            To(m_buffer,0,c,out p_value); 
        }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,float p_value) { return From(p_buffer,(double)p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,float p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }

        /// <summary>
        /// Writes the value into a byte array in its binary representation
        /// </summary>
        /// <param name="p_buffer"></param>
        /// <param name="p_value"></param>
        /// <returns></returns>
        static public int From(byte[] p_buffer,float p_value) { 
            UIntToFloat b; 
            b.Byte0=b.Byte1=b.Byte2=b.Byte3=0;
            b.Value     = p_value; 
            p_buffer[0] = b.Byte0;
            p_buffer[1] = b.Byte1;
            p_buffer[2] = b.Byte2;
            p_buffer[3] = b.Byte3;
            return 4;
        }

        #endregion

        #region double

        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out double p_value) {
            if(p_buffer[0]=='+')if(p_buffer[1]=='I') { p_value = double.PositiveInfinity; return; }
            if(p_buffer[0]=='-')if(p_buffer[1]=='I') { p_value = double.NegativeInfinity; return; }
            if(p_buffer[0]=='N')                     { p_value = double.NaN;              return; }
            int  k  = 0;
            bool sn = false;
            for(int i = 0; i < p_buffer.Length; i++) {
                if(p_buffer[i]<=0) break;
                k++;
                if(p_buffer[i]=='E') { sn = true; }
            }
            //Too lazy to parse scientific notation
            if(sn) { p_value = double.Parse(new string(p_buffer,0,k)); return; }
            bool nf = false;
            double vi,vf;
            int vflz;
            Extract(p_buffer,p_offset,p_count,out nf,out vi,out vf,out vflz);
            double di  = (double)vi;
            int    dfd = DigitCount((ulong)vf);
            //double df  = ((double)vf)/Math.Pow(10.0,(double)dfd);
            double df  = ((double)vf)/pow_cache[dfd];
            //if(vflz>0) df /= Math.Pow(10.0,(double)vflz);
            if(vflz>0) df /= pow_cache[vflz];            
            p_value = nf ? -(di+df) : (di+df);
        }
        

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out double p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out double p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,double p_value,int p_precision=0) {
            if(double.IsNaN(p_value))              { p_buffer[0] = 'N'; return 1; }
            if(double.IsPositiveInfinity(p_value)) { p_buffer[0] = '+'; p_buffer[1] = 'I'; return 2; }
            if(double.IsNegativeInfinity(p_value)) { p_buffer[0] = '-'; p_buffer[1] = 'I'; return 2; }
            double v    = p_value;
            bool   neg  = v<0.0;
            if(v<0.0) v=-v;
            double dpi  = Math.Floor(v);                        
            double dpf  = v - dpi;            
            int lz = -1; //ignore first place
            double lz_dpf=dpf;
            if(lz_dpf>0)
            while(lz_dpf < 1.0) { 
                lz++; 
                lz_dpf*=10.0;                 
            }            
            ulong  pi   = (ulong)dpi;            
            double pm   = p_precision>0 ? pow_cache[p_precision] : 1000000000000000000.0;            
            dpf         = p_precision>0 ? Math.Round(dpf*pm) : (dpf*pm);
            ulong  pf   = (ulong)(dpf);
            while((pf % 10) <= 0) {
                if(pf<=0)break;
                pf /= 10;
            }
            int len=0;
            if(dpi<=0)if(dpf<=0) neg=false;
            if(neg)p_buffer[0]='-';
            len += NumberToChars(p_buffer,pi,neg ? 1 : 0);
            p_buffer[len++]=m_digits_sep;
            if(pf>0)
            for(int i = 0; i < lz; i++) { p_buffer[len++]='0'; }
            len = NumberToChars(p_buffer,pf,len);
            return len;
        }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,double p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }

        /// <summary>
        /// Writes the value into a byte array in its binary representation
        /// </summary>
        /// <param name="p_buffer">Byte buffer to write into</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of bytes written</returns>
        static public int From(byte[] p_buffer,double p_value) { 
            ULongToDouble b; 
            b.Byte0=b.Byte1=b.Byte2=b.Byte3=b.Byte4=b.Byte5=b.Byte6=b.Byte7=0;
            b.Value     = p_value; 
            p_buffer[0] = b.Byte0;
            p_buffer[1] = b.Byte1;
            p_buffer[2] = b.Byte2;
            p_buffer[3] = b.Byte3;
            p_buffer[4] = b.Byte4;
            p_buffer[5] = b.Byte5;
            p_buffer[6] = b.Byte6;
            p_buffer[7] = b.Byte7;
            return 8;
        }
        #endregion

        #region DateTime
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out DateTime p_value) { 
            long v; 
            To(p_buffer,p_offset,p_count,out v); 
            p_value = DateTime.FromBinary(v); 
        }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out DateTime p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out DateTime p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,DateTime p_value) { return From(p_buffer,p_value.ToBinary()); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,DateTime p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region TimeSpan
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To(char[] p_buffer,int p_offset,int p_count,out TimeSpan p_value) { long v; To(p_buffer,p_offset,p_count,out v); p_value = new TimeSpan(v); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(StringBuilder p_buffer,out TimeSpan p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To(string p_buffer,out TimeSpan p_value) { int c=PopulateBuffer(p_buffer); To(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,TimeSpan p_value) { return From(p_buffer,p_value.Ticks); }
        
        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,TimeSpan p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #region Enum
        /// <summary>
        /// Reads the char buffer and returns its parsed value.
        /// </summary>
        /// <param name="p_buffer">Buffer to read from</param>
        /// <param name="p_value">Parsed value.</param>        
        static public void To<T>(char[] p_buffer,int p_offset,int p_count,out T p_value) where T : Enum { long v; To(p_buffer,p_offset,p_count,out v); p_value = (T)Enum.ToObject(typeof(T),v); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To<T>(StringBuilder p_buffer,out T p_value) where T : Enum { int c=PopulateBuffer(p_buffer); To<T>(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Reads a bool from the buffer and returns its value.
        /// </summary>
        /// <param name="p_buffer">Chars with the data</param>
        /// <returns>Parsed value</returns>
        static public void To<T>(string p_buffer,out T p_value) where T : Enum { int c=PopulateBuffer(p_buffer); To<T>(m_buffer,0,c,out p_value); }

        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(char[] p_buffer,Enum p_value) { 
            Type e_type = Enum.GetUnderlyingType(p_value.GetType());
            if(e_type == typeof(sbyte   )) return From(p_buffer,(sbyte   )(object)p_value); else
            if(e_type == typeof(byte    )) return From(p_buffer,(byte    )(object)p_value); else
            if(e_type == typeof(short   )) return From(p_buffer,(short   )(object)p_value); else
            if(e_type == typeof(ushort  )) return From(p_buffer,(ushort  )(object)p_value); else
            if(e_type == typeof(int     )) return From(p_buffer,(int     )(object)p_value); else
            if(e_type == typeof(uint    )) return From(p_buffer,(uint    )(object)p_value); else
            if(e_type == typeof(long    )) return From(p_buffer,(long    )(object)p_value); else
            if(e_type == typeof(ulong   )) return From(p_buffer,(ulong   )(object)p_value); else
            if(e_type == typeof(decimal )) return From(p_buffer,(decimal )(object)p_value);
            return 0;
        }
        
        /// <summary>
        /// Writes a value in the buffer and returns the number of char written.
        /// </summary>
        /// <param name="p_buffer">Char array to be written.</param>
        /// <param name="p_value">Value to write</param>
        /// <returns>Number of char written.</returns>
        static public int From(StringBuilder p_buffer,Enum p_value) { PopulateBuffer(p_buffer); int c=From(m_buffer,p_value); p_buffer.Clear(); p_buffer.Append(m_buffer,0,c); return c; }
        #endregion

        #endregion

        /// <summary>
        /// Generates a concatenated date hash for '.UtcNow' variable.
        /// </summary>
        /// <returns>A string composed of the date-time parts as a YYMMDD... hash</returns>
        static public string DateHash(string p_yf="yyyy",string p_mf = "MM",string p_df="dd",string p_thf="HH",string p_tmf="mm",string p_tsf="ss",string p_separator="") {
            return DateHash(DateTime.UtcNow,p_yf,p_mf,p_df,p_thf,p_tmf,p_tsf,p_separator);
        }

        /// <summary>
        /// Generates a concatenated date hash for the DateTime informed.
        /// </summary>
        /// <param name="p_date">DateTime to extract the hash components.</param>
        /// <returns>A string composed of the date-time parts as a YYMMDD... hash</returns>
        static public string DateHash(System.DateTime p_date,string p_yf="yyyy",string p_mf = "MM",string p_df="dd",string p_thf="HH",string p_tmf="mm",string p_tsf="ss",string p_separator="") {
            StringBuilder sb = new StringBuilder();
            bool f = false;
            string sep = p_separator;
            if(!string.IsNullOrEmpty(p_yf))  {                       sb.Append(p_date.ToString(p_yf));  f = true; }
            if(!string.IsNullOrEmpty(p_mf))  { if(f) sb.Append(sep); sb.Append(p_date.ToString(p_mf));  f = true; }
            if(!string.IsNullOrEmpty(p_df))  { if(f) sb.Append(sep); sb.Append(p_date.ToString(p_df));  f = true; }
            if(!string.IsNullOrEmpty(p_thf)) { if(f) sb.Append(sep); sb.Append(p_date.ToString(p_thf)); f = true; } 
            if(!string.IsNullOrEmpty(p_tmf)) { if(f) sb.Append(sep); sb.Append(p_date.ToString(p_tmf)); f = true; }
            if(!string.IsNullOrEmpty(p_tsf)) { if(f) sb.Append(sep); sb.Append(p_date.ToString(p_tsf)); f = true; }                     
            return sb.ToString();
        }

        /// <summary>
        /// Returns the ordinal suffix.
        /// </summary>
        /// <param name="p_number">Number to have its ordinal sampled</param>
        /// <returns>Ordinal suffix string</returns>
        static public string Ordinal(int p_number) {
            int n = p_number;            
            switch(n%24) {                
                case 1:  return "st";
                case 2:  return "nd";
                case 3:  return "rd";
                case 21: return "st";
                case 22: return "nd";
                case 23: return "rd";
            }
            return "th";
        }

    }
}
