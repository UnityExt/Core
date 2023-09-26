using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Math = System.Math;

namespace UnityExt.Core { 

    /// <summary>
    /// Class that abstract bitwise manipulation and storage.
    /// </summary>
    public class BitStream : IDisposable  {

        /// <summary>
        /// Reference to the associated stream.
        /// </summary>
        public Stream BaseStream { get; protected set; }

        /// <summary>
        /// Returns the base stream cast to the desired type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetStream<T>() where T : Stream { return (BaseStream is T) ? (BaseStream as T) : null;  }

        /// <summary>
        /// Currently being written bit.
        /// </summary>
        public long BitPosition {
            get { return m_bit_p; }
            set {                                

                if(!m_has_stream) return;

                long bit_c  = m_bit_p;
                long byte_c = bit_c>>3;
                long bit_n  = Math.Max(0,value);                                                
                long byte_n = bit_n>>3;
                
                m_bit_p  = bit_n;
                m_byte_p = byte_n;

                if(byte_c==byte_n) return;

                if(m_bit_dirty)
                if(m_can_write) {                            
                    BaseStream.Write(m_buffer,0,m_buffer.Length);                    
                    byte_c = m_can_seek ? BaseStream.Position : byte_c+1;
                }

                m_bit_dirty=false;
                Array.Clear(m_buffer,0,m_buffer.Length);

                StreamSeek(byte_n - byte_c);

                if(m_can_read) {                                     
                    BaseStream.Read(m_buffer,0,m_buffer.Length);
                    byte_c = m_can_seek ? BaseStream.Position : byte_c+1;
                }
                             
                StreamSeek(byte_n - byte_c);

            }
        }        
        private void StreamSeek(long o) { if(m_can_seek) if(o!=0) { BaseStream.Seek(o, SeekOrigin.Current); } }
        private long m_bit_p;
        private long m_byte_p;
        private bool m_bit_dirty;

        /// <summary>
        /// Returns the amount of bits available in the stream.
        /// </summary>
        public long BitCapacity { 
            get { 
                if(!m_has_stream) return 0;
                if(m_is_mstream)  return m_mstream.Capacity << 3;
                if(m_is_fstream)  return m_fstream.Length   << 3;
                return 0;
            }
        }

        /// <summary>
        /// Returns the amount of bits written in the stream.
        /// </summary>
        public long BitLength { 
            get { 
                if(!m_has_stream) return 0;
                if(m_is_mstream)  return m_mstream.Length << 3;
                if(m_is_fstream)  return m_fstream.Length << 3;
                return 0;
            }
        }

        /// <summary>
        /// Flag that tells this stream can be written.
        /// </summary>
        public bool CanWrite { get { return m_can_write; } }
        private bool m_can_write;

        /// <summary>
        /// Flag that tells this stream can be read.
        /// </summary>
        public bool CanRead  { get { return m_can_read;  } }
        private bool m_can_read;

        /// <summary>
        /// Flag that tells this stream can be seeked.
        /// </summary>
        public bool CanSeek  { get { return m_can_seek;  } }
        private bool m_can_seek;

        /// <summary>
        /// Flag that tells there is a valid stream.
        /// </summary>
        public bool HasStream { get { return m_has_stream; } }
        private bool m_has_stream;
        private bool m_is_mstream;
        private bool m_is_fstream;
        private MemoryStream m_mstream;
        private FileStream   m_fstream;

        /// <summary>
        /// Byte buffer being written
        /// </summary>        
        private byte[] m_buffer;        
        
        /// <summary>
        /// Internal CTOR.
        /// </summary>
        private BitStream() {
            m_is_mstream    = false;
            m_is_fstream    = false;
            m_can_write     = false;
            m_can_read      = false;
            m_can_seek      = false;
            m_buffer        = new byte[1];   
            m_bit_dirty     = false;    
        }

        /// <summary>
        /// BitStream constructor receiving an underlaying stream to operate.
        /// </summary>
        /// <param name="p_stream">BaseStream to operate.</param>
        /// <param name="p_bit_offset">Bit offset starting from the stream current Position</param>
        public BitStream(Stream p_stream,long p_bit_offset) : this() {

            BaseStream      = p_stream;            

            m_has_stream    = BaseStream!=null;       

            long s_pos      = 0;            

            if(m_has_stream) {
                m_is_mstream    = BaseStream is MemoryStream;
                m_is_fstream    = BaseStream is   FileStream;
                if(m_is_mstream) m_mstream = BaseStream as MemoryStream;
                if(m_is_fstream) m_fstream = BaseStream as   FileStream;
                m_can_write     = BaseStream.CanWrite;
                m_can_read      = BaseStream.CanRead;
                m_can_seek      = BaseStream.CanSeek;
                s_pos           = m_can_seek ? BaseStream.Position : 0;
            }   
            
            m_bit_p         = s_pos   << 3;
            m_byte_p        = m_bit_p >> 3;
            
            if(!m_has_stream) return;
            if(!m_can_read)   return;
            
            BaseStream.Read(m_buffer,0,m_buffer.Length);       
            
            if(!m_can_seek)   return;

            long s_off  = m_can_seek ? (BaseStream.Position-s_pos) : 0;
            s_pos       = m_can_seek ? BaseStream.Position         : 0;
            StreamSeek(-s_off);
        }

        /// <summary>
        /// BitStream constructor receiving an underlaying stream to operate.
        /// </summary>
        /// <param name="p_stream">BaseStream to operate.</param>
        public BitStream(Stream p_stream) : this(p_stream,0) { }

        /// <summary>
        /// BitStream constructor that allocates a memory stream sized to allocate the desired number of bits.
        /// </summary>
        /// <param name="p_bit_length"></param>
        public BitStream(ulong p_bit_length) : this(new MemoryStream((int)(p_bit_length<=0 ? 0 : p_bit_length/8))) { }
        
        #region WriteBit

        /// <summary>
        /// Writes a single bit.
        /// </summary>
        /// <param name="p_value">Bit flag as boolean.</param>
        public void WriteBit(bool p_value) { if(m_can_write) InternalWriteBit(p_value); }
        
        /// <summary>
        /// Writes a single bit.
        /// </summary>
        /// <param name="p_value">Bit flag as 0 or non-0 number</param>
        public void WriteBit(int p_value) { if(m_can_write) InternalWriteBit(p_value!=0); }

        /// <summary>
        /// Writes an array of bits.
        /// </summary>
        /// <param name="p_values">Bits encoded as 'true' or 'false'</param>
        public void WriteBits(bool[] p_values) { if(m_can_write) for(int i=0;i<p_values.Length;i++) InternalWriteBit(p_values[i]); }

        /// <summary>
        /// Writes an array of bits.
        /// </summary>
        /// <param name="p_values">Bits encoded as 0 and 1</param>
        public void WriteBits(byte[] p_values) { if(m_can_write) for(int i=0;i<p_values.Length;i++) InternalWriteBit(p_values[i]==1); }

        /// <summary>
        /// Writes an array of bits.
        /// </summary>
        /// <param name="p_values">Bits encoded as '0' and '1' chars</param>
        public void WriteBits(string p_values) { if(m_can_write) for(int i=0;i<p_values.Length;i++) InternalWriteBit(p_values[i]=='1'); }

        /// <summary>
        /// Writes an array of bits.
        /// </summary>
        /// <param name="p_values">Bits encoded as '0' and '1' chars</param>
        public void WriteBits(char[] p_values) { if(m_can_write) for(int i=0;i<p_values.Length;i++) InternalWriteBit(p_values[i]=='1'); }

        /// <summary>
        /// Internal method to write a single bit.
        /// </summary>
        /// <param name="f"></param>
        private void InternalWriteBit(bool f) {
            if(!m_can_write) return;
            long bp  = m_bit_p>>3;
            long bi  = bp % m_buffer.Length;
            int  p   = ((int)m_bit_p & 7);
            long msk = (128 >> p);
            long v   = m_buffer[bi];
            v = (byte)(f ? (v | msk) : (v & ~msk));            
            m_buffer[bi] = (byte)v;
            m_bit_dirty=true;            
            BitPosition++;
        }

        #endregion

        #region Write

        /// <summary>
        /// Writes a 64 bit mask.
        /// </summary>
        /// <param name="p_value">Bit mask number.</param>
        /// <param name="p_bits">Number of bits to write.</param>
        /// <param name="p_lsb">Flag telling to write based on LeastSignificantBit or MostSignificantBit</param>
        public void Write(ulong p_value,int p_bits=64,bool p_lsb=false) { if(m_can_write) InternalWrite(p_value,64,p_bits,p_lsb); }

        /// <summary>
        /// Writes a 32 bit mask.
        /// </summary>
        /// <param name="p_value">Bit mask number.</param>
        /// <param name="p_bits">Number of bits to write.</param>
        /// <param name="p_lsb">Flag telling to write based on LeastSignificantBit or MostSignificantBit</param>
        public void Write(uint p_value,int p_bits=32,bool p_lsb=false) { if(m_can_write) InternalWrite(p_value,32,p_bits,p_lsb); }

        /// <summary>
        /// Writes a 16 bit mask.
        /// </summary>
        /// <param name="p_value">Bit mask number.</param>
        /// <param name="p_bits">Number of bits to write.</param>
        /// <param name="p_lsb">Flag telling to write based on LeastSignificantBit or MostSignificantBit</param>
        public void Write(ushort p_value,int p_bits=16,bool p_lsb=false) { if(m_can_write) InternalWrite(p_value,16,p_bits,p_lsb); }

        /// <summary>
        /// Writes a 8 bit mask.
        /// </summary>
        /// <param name="p_value">Bit mask number.</param>
        /// <param name="p_bits">Number of bits to write.</param>
        /// <param name="p_lsb">Flag telling to write based on LeastSignificantBit or MostSignificantBit</param>
        public void Write(byte p_value,int p_bits=8,bool p_lsb=false) { if(m_can_write) InternalWrite(p_value,8,p_bits,p_lsb); }

        /// <summary>
        /// Writes a 8 bit mask.
        /// </summary>
        /// <param name="p_value">Bit mask number.</param>
        /// <param name="p_bits">Number of bits to write.</param>
        /// <param name="p_lsb">Flag telling to write based on LeastSignificantBit or MostSignificantBit</param>
        public void Write(char p_value,int p_bits=8,bool p_lsb=false) { if(m_can_write) InternalWrite(p_value,8,p_bits,p_lsb); }

        /// <summary>
        /// Writes each string's char as an 8 bit mask.
        /// </summary>
        /// <param name="p_value">String to write each char as 8 bit mask.</param>
        public void Write(string p_value) { if(m_can_write) for(int i=0;i<p_value.Length;i++) Write(p_value[i],8,false); }

        /// <summary>
        /// Writes a floating point number mapping its value to a range encoded in the specified number of bits.
        /// If no bits are specified the range size is used to estimate max number of bits.
        /// </summary>
        /// <param name="p_value">Number to be bit encoded</param>
        /// <param name="p_min">Minimum expected value</param>
        /// <param name="p_max">Maximum expected value</param>
        /// <param name="p_bits">Number of bits to store the number</param>
        public void Write(float p_value,float p_min,float p_max,int p_bits=0) {
            float  vmax    = p_max<p_min ? p_min : p_max;
            float  vmin    = p_max<p_min ? p_max : p_min;
            float  rng     = vmax-vmin;
            int    bc      = p_bits>0 ? p_bits : GetMSB((ulong)rng)+1;
            if(bc<=0) bc = 1;
            float  rmax    = (float)((1<<bc)-1);
            float  r       = rng<=0f   ? 0 : ((p_value-vmin) / rng);
            ulong  v       = (ulong)((rmax * (r<0.0f ? 0.0f : (r>1.0f ? 1.0f : r)))+0.5f);
            Write(v,bc,true);
        }

        /// <summary>
        /// Writes a floating point number mapping its value to a range encoded in the specified number of bits.
        /// If no bits are specified the range size is used to estimate max number of bits.
        /// </summary>
        /// <param name="p_value">Number to be bit encoded</param>
        /// <param name="p_min">Minimum expected value</param>
        /// <param name="p_max">Maximum expected value</param>
        /// <param name="p_bits">Number of bits to store the number</param>
        public void Write(double p_value,double p_min,double p_max,int p_bits=0) {
            double vmax    = p_max<p_min ? p_min : p_max;
            double vmin    = p_max<p_min ? p_max : p_min;
            double rng     = vmax-vmin;
            int    bc      = p_bits>0 ? p_bits : GetMSB((ulong)rng)+1;
            if(bc<=0) bc = 1;
            double rmax    = (double)((1<<bc)-1);
            double r       = rng<=0f   ? 0 : ((p_value-vmin) / rng);            
            ulong  v       = (ulong)((rmax * (r<0.0 ? 0.0 : (r>1.0 ? 1.0 : r)))+0.5);
            Write(v,bc,true);
        }
        
        /// <summary>
        /// Helper to write bits from numbers
        /// </summary>
        /// <param name="v"></param>
        /// <param name="bm"></param>
        /// <param name="bc"></param>
        /// <param name="lsb"></param>
        private void InternalWrite(ulong v,int bm,int bc,bool lsb) {
            int bo = lsb ? bc : bm;
            if(bo<1) bo=1;
            ulong msk = (ulong)(1<<(bo-1));
            for(int i=0;i<bc;i++) {
                bool f = (v & msk)!=0;
                WriteBit(f);
                msk = msk>>1;
            }
        }

        #endregion

        #region ReadBit

        /// <summary>
        /// Reads a single bit.
        /// </summary>
        /// <returns></returns>
        public bool ReadBit() {
            if(!m_can_read) return false;
            long bi  = (m_bit_p>>3) % m_buffer.Length;
            int  p   = ((int)m_bit_p & 7);            
            long msk = (128 >> p);
            byte v   = (byte)m_buffer[bi];            
            BitPosition++;            
            return (v & msk)!=0;
        }

        /// <summary>
        /// Reads all remaining bits and returns as a bool array.
        /// </summary>
        /// <returns></returns>
        public bool[] ReadBits() {                  
            long bit_total = Math.Max(0,BitLength-BitPosition);
            bool[] res = new bool[bit_total];
            if(!m_can_read) return res;            
            for(int i = 0; i<bit_total; i++) { res[i] = ReadBit(); }
            return res;
        }

        #endregion

        #region Read

        /// <summary>
        /// Reads a slice of the current bit position into a number.
        /// </summary>
        /// <param name="p_value">64 bit number container.</param>
        /// <param name="p_bits">Number of bits to write</param>
        /// <param name="p_lsb">Significant bit starting point.</param>
        public void Read(out ulong p_value,int p_bits=64,bool p_lsb=false) { ulong v = p_value = 0; if(m_can_read)  { InternalRead(out v,64,p_bits,p_lsb); p_value = (ulong)v; } }

        /// <summary>
        /// Reads a slice of the current bit position into a number.
        /// </summary>
        /// <param name="p_value">32 bit number container.</param>
        /// <param name="p_bits">Number of bits to write</param>
        /// <param name="p_lsb">Significant bit starting point.</param>
        public void Read(out uint p_value,int p_bits=32,bool p_lsb=false) { ulong v = p_value = 0; if(m_can_read)  { InternalRead(out v,32,p_bits,p_lsb); p_value = (uint)v; } }

        /// <summary>
        /// Reads a slice of the current bit position into a number.
        /// </summary>
        /// <param name="p_value">16 bit number container.</param>
        /// <param name="p_count">Number of bits to write</param>
        /// <param name="p_lsb">Significant bit starting point.</param>
        public void Read(out ushort p_value,int p_bits=16,bool p_lsb=false) { ulong v = p_value = 0; if(m_can_read) { InternalRead(out v,16,p_bits,p_lsb); p_value = (ushort)v; } }

        /// <summary>
        /// Reads a slice of the current bit position into a number.
        /// </summary>
        /// <param name="p_value">8 bit number container.</param>
        /// <param name="p_bits">Number of bits to write</param>
        /// <param name="p_lsb">Significant bit starting point.</param>
        public void Read(out byte p_value,int p_bits=8,bool p_lsb=false) { ulong v = p_value = 0; if(m_can_read) { InternalRead(out v,8,p_bits,p_lsb); p_value = (byte)v; } }

        /// <summary>
        /// Reads a slice of the current bit position into a number.
        /// </summary>
        /// <param name="p_value">8 bit number container.</param>
        /// <param name="p_bits">Number of bits to write</param>
        /// <param name="p_lsb">Significant bit starting point.</param>
        public void Read(out char p_value,int p_bits=8,bool p_lsb=false) { ulong v = p_value = '\0'; if(m_can_read) { InternalRead(out v,8,p_bits,p_lsb); p_value = (char)v; } }

        /// <summary>
        /// Reads a floating point number mapping its value to a range encoded in the specified number of bits.
        /// If no bits are specified the range size is used to estimate max number of bits.
        /// </summary>
        /// <param name="p_value">Number to be read</param>
        /// <param name="p_min">Minimum expected value</param>
        /// <param name="p_max">Maximum expected value</param>
        /// <param name="p_bits">Number of bits used to store the number</param>
        public void Read(out float p_value,float p_min,float p_max,int p_bits=0) {
            float  vmax    = p_max<p_min ? p_min : p_max;
            float  vmin    = p_max<p_min ? p_max : p_min;
            float  rng     = vmax-vmin;
            int    bc      = p_bits>0 ? p_bits : GetMSB((ulong)rng)+1;
            if(bc<=0) bc = 1;
            float  rmax    = (float)((1<<bc)-1);
            ulong  v       = 0;
            Read(out v,bc,true);
            float  r       = rmax<=0 ? 0 : ((float)v)/rmax;            
            p_value        = vmin + rng*(r<0.0f ? 0.0f : (r>1.0f ? 1.0f : r));
        }

        /// <summary>
        /// Reads a floating point number mapping its value to a range encoded in the specified number of bits.
        /// If no bits are specified the range size is used to estimate max number of bits.
        /// </summary>
        /// <param name="p_value">Number to be read</param>
        /// <param name="p_min">Minimum expected value</param>
        /// <param name="p_max">Maximum expected value</param>
        /// <param name="p_bits">Number of bits used to store the number</param>
        public void Read(out double p_value,double p_min,double p_max,int p_bits=0) {
            double vmax    = p_max<p_min ? p_min : p_max;
            double vmin    = p_max<p_min ? p_max : p_min;
            double rng     = vmax-vmin;
            int    bc      = p_bits>0 ? p_bits : GetMSB((ulong)rng)+1;
            if(bc<=0) bc = 1;
            double rmax    = (double)((1<<bc)-1);
            ulong  v       = 0;
            Read(out v,bc,true);
            double r       = rmax<=0 ? 0 : ((double)v)/rmax;
            p_value        = vmin + rng*(r<0.0 ? 0.0 : (r>1.0 ? 1.0 : r));
        }

        /// <summary>
        /// Helper to read bits into a long value for later transfer.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="bm"></param>
        /// <param name="bc"></param>
        /// <param name="lsb"></param>
        private void InternalRead(out ulong v,int bm,int bc,bool lsb) { 
            v=0;
            int bo = lsb ? bc : bm;
            if(bo<1) bo=1;            
            ulong msk = (ulong)(1<<(bo-1));
            for(int i=0;i<bc;i++) {
                bool f = ReadBit();
                v = f ? (v | msk) : (v & ~msk);
                msk = msk>>1;
            }            
        }

        #endregion

        /// <summary>
        /// Sets this stream length in bit count.
        /// </summary>
        /// <param name="p_bit_length"></param>
        public void SetLength(long p_bit_length) {
            if(!m_has_stream) return;
            long len = p_bit_length<=0 ? 0 : ((p_bit_length>>3)+1);
            BaseStream.SetLength(len);
        }

        /// <summary>
        /// Closes the stream.
        /// </summary>
        public void Close() { 
            if(m_has_stream) BaseStream.Close(); 
        }

        /// <summary>
        /// Flushes the stream.
        /// </summary>
        public void Flush() {            
            if(!m_has_stream) return;
            if(m_can_write)
            if(m_bit_dirty) {
                m_bit_dirty=false;
                BaseStream.Write(m_buffer,0,m_buffer.Length);
                if(m_can_seek)BaseStream.Seek(-m_buffer.Length,SeekOrigin.Current);
            }
            BaseStream.Flush();
        }

        /// <summary>
        /// Async flushes the stream.
        /// </summary>
        public async Task FlushAsync() {
            if(!m_has_stream) return;
            if(m_can_write)
            if(m_bit_dirty) {
                m_bit_dirty=false;
                await BaseStream.WriteAsync(m_buffer,0,m_buffer.Length);
                if(m_can_seek)BaseStream.Seek(-m_buffer.Length,SeekOrigin.Current);
            }
            await BaseStream.FlushAsync();
        }

        /// <summary>
        /// Seeks a new bit position.
        /// </summary>
        /// <param name="p_bit_offset">Bit offset to move from search position.</param>
        /// <param name="p_origin">search index origin point.</param>
        public void Seek(long p_bit_offset, SeekOrigin p_origin) {
            long p = 0;
            long o = 0;
            switch(p_origin) {
                case SeekOrigin.Begin:   p = 0;             o =  p_bit_offset; break;
                case SeekOrigin.Current: p = m_bit_p;       o =  p_bit_offset; break;
                case SeekOrigin.End:     p = BitCapacity-1; o = -p_bit_offset; break;
            }
            BitPosition = p + o;
        }

        /// <summary>
        /// Seeks a new bit position starting from the begining.
        /// </summary>
        /// <param name="p_bit_offset">Bit offset to seek</param>
        public void Seek(long p_bit_offset) { Seek(p_bit_offset, SeekOrigin.Begin); }

        /// <summary>
        /// Disposes all resources.
        /// </summary>
        public void Dispose() {            
            m_bit_p = 0;
            if(m_has_stream) BaseStream.Dispose();
        }

        /// <summary>
        /// Auxiliary method to return the most significant bit of a number.
        /// Credits: https://stackoverflow.com/a/31377558
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        static public int GetMSB(ulong v) {
            if(v<=0) return 1;
            ulong n = 1;
            if ((v >> 32) == 0) { n = n + 32; v = v << 32; }
            if ((v >> 48) == 0) { n = n + 16; v = v << 16; }
            if ((v >> 56) == 0) { n = n +  8; v = v <<  8; }
            if ((v >> 60) == 0) { n = n +  4; v = v <<  4; }
            if ((v >> 62) == 0) { n = n +  2; v = v <<  2; }
            n = n - (v >> 63);
            return (int)(64-n);
        }

        /// <summary>
        /// Returns the written bits as string.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            if(m_sb==null) m_sb = new StringBuilder();
            long bit_pos = BitPosition;
            if(m_can_seek) BitPosition=0;            
            if(m_can_read) {                                 
                for(int i=0;i<bit_pos;i++) { m_sb.Append(ReadBit() ? '1' : '0'); }
            }
            if(m_can_seek) BitPosition=bit_pos;
            return m_sb.ToString();
        }
        private StringBuilder m_sb;

    }
}