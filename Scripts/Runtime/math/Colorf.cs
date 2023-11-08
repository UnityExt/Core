using UnityEngine;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace UnityExt.Core {

    /// <summary>
    /// Color functions helper class.
    /// </summary>
    public static class Colorf {

        /// <summary>
        /// One over 255
        /// </summary>
        internal const float InvByte = 0.003921568627451f;

        #region ColorExt
        static string[] m_byte_hex_lut = new string[255];
        
        /// <summary>
        /// CTOR
        /// </summary>
        static Colorf() {
            for(int i=0;i<255;i++) m_byte_hex_lut[i] = i.ToString("x2");
        }

        #region Color <-> Color32
        /// <summary>
        /// Converts Unity's Color32 to Color
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public Color ToColor(this Color32 c) { return Colorf.ARGBToColor(c.a, c.r, c.g, c.b); }

        /// <summary>
        /// Converts Unity's Color to Color32
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public Color32 ToColor32(this Color c) { return new Color32(c.redByte(), c.greenByte(), c.blueByte(), c.alphaByte()); }
        #endregion

        #region Color <-> channel byte
        /// <summary>
        /// Red channel as byte.
        /// </summary>
        /// <param name="c">Color instance</param>
        /// <returns></returns>
        static public byte redByte(this Color c) { return (byte)(c.r*255f); }

        /// <summary>
        /// Green channel as byte.
        /// </summary>
        /// <param name="c">Color instance</param>
        /// <returns></returns>
        static public byte greenByte(this Color c) { return (byte)(c.g*255f); }

        /// <summary>
        /// Blue channel as byte.
        /// </summary>
        /// <param name="c">Color instance</param>
        /// <returns></returns>
        static public byte blueByte(this Color c) { return (byte)(c.b*255f); }

        /// <summary>
        /// Alpha channel as byte.
        /// </summary>
        /// <param name="c">Color instance</param>
        /// <returns></returns>
        static public byte alphaByte(this Color c) { return (byte)(c.a*255f); }
        #endregion

        #region Color -> uint
        /// <summary>
        /// Converts a Color class to 32 bit ARGB color.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public uint argb(this Color c) { return (uint)((c.alphaByte() << 24)|(c.redByte() << 16)|(c.greenByte() << 8)|(c.blueByte())); }

        /// <summary>
        /// Converts a Color class to 32 bit RGBA color.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public uint rgba(this Color c) { return (uint)((c.redByte() << 24)|(c.greenByte() << 16)|(c.blueByte() << 8)|(c.alphaByte())); }

        /// <summary>
        /// Converts a Color class to 24 bit RGB color.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public uint rgb(this Color c) { return (uint)((c.redByte() << 16)|(c.greenByte() << 8)|(c.blueByte())); }
        #endregion

        #region Color -> byte[]
        /// <summary>
        /// Returns RGB channels as bytes.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public byte[] rgbBytes(this Color c) { return new byte[] { c.redByte(), c.greenByte(), c.blueByte() }; }

        /// <summary>
        /// Returns RGBA channels as bytes.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public byte[] rgbaBytes(this Color c) { return new byte[] { c.redByte(), c.greenByte(), c.blueByte(), c.alphaByte() }; }

        /// <summary>
        /// Returns ARGB channels as bytes.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static public byte[] argbBytes(this Color c) { return new byte[] { c.alphaByte(),c.redByte(), c.greenByte(), c.blueByte() }; }
        #endregion

        #region Color -> Hex
        static private string ToHex(string pfx,byte v0,byte v1,byte v2)         { StringBuilder sb = new StringBuilder(); sb.Append(pfx); sb.Append(m_byte_hex_lut[v0]); sb.Append(m_byte_hex_lut[v1]); sb.Append(m_byte_hex_lut[v2]); return sb.ToString(); }
        static private string ToHex(string pfx,byte v0,byte v1,byte v2,byte v3) { StringBuilder sb = new StringBuilder(); sb.Append(pfx); sb.Append(m_byte_hex_lut[v0]); sb.Append(m_byte_hex_lut[v1]); sb.Append(m_byte_hex_lut[v2]); sb.Append(m_byte_hex_lut[v3]); return sb.ToString(); }

        /// <summary>
        /// Returns a hexadecimal color string.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="p_prefix"></param>
        /// <returns></returns>
        static public string ToRGBHex(this Color c,string p_prefix="") { return ToHex(p_prefix,c.redByte(),c.greenByte(),c.blueByte()); }

        /// <summary>
        /// Returns a hexadecimal color string.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="p_prefix"></param>
        /// <returns></returns>
        static public string ToARGBHex(this Color c,string p_prefix="") { return ToHex(p_prefix,c.alphaByte(),c.redByte(),c.greenByte(),c.blueByte()); }

        /// <summary>
        /// Returns a hexadecimal color string.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="p_prefix"></param>
        /// <returns></returns>
        static public string ToRGBAHex(this Color c,string p_prefix="") { return ToHex(p_prefix,c.redByte(),c.greenByte(),c.blueByte(),c.alphaByte()); }
        #endregion
        #endregion
        
        #region Color Consts
        /// <summary>
        /// Returns a white transparent color
        /// </summary>
        public static Color transparent { get { return new Color(1f,1f,1f,0f); } }

        /// <summary>
        /// 25% Red
        /// </summary>
        public static Color red25 { get { return new Color(0.25f,0f,0f,1f); } }

        /// <summary>
        /// 50% Red
        /// </summary>
        public static Color red50 { get { return new Color(0.5f,0f,0f,1f); } }

        /// <summary>
        /// 75% Red
        /// </summary>
        public static Color red75 { get { return new Color(0.75f,0f,0f,1f); } }

        /// <summary>
        /// 25% Green
        /// </summary>
        public static Color green25 { get { return new Color(0f,0.25f,0f,1f); } }

        /// <summary>
        /// 50% Green
        /// </summary>
        public static Color green50 { get { return new Color(0f,0.5f,0f,1f); } }

        /// <summary>
        /// 75% Green
        /// </summary>
        public static Color green75 { get { return new Color(0f,0.75f,0f,1f); } }

        /// <summary>
        /// 25% Blue
        /// </summary>
        public static Color blue25 { get { return new Color(0f,0f,0.25f,1f); } }

        /// <summary>
        /// 50% Blue
        /// </summary>
        public static Color blue50 { get { return new Color(0f,0f,0.5f,1f); } }

        /// <summary>
        /// 75% Blue
        /// </summary>
        public static Color blue75 { get { return new Color(0f,0f,0.75f,1f); } }

        /// <summary>
        /// 25% Yellow
        /// </summary>
        public static Color yellow25 { get { return new Color(0.25f, 0.25f, 0f, 1f); } }

        /// <summary>
        /// 50% Yellow
        /// </summary>
        public static Color yellow50 { get { return new Color(0.5f, 0.5f, 0f, 1f); } }

        /// <summary>
        /// 75% Yellow
        /// </summary>
        public static Color yellow75 { get { return new Color(0.75f, 0.75f, 0f, 1f); } }

        /// <summary>
        /// Blue for GUI Focus
        /// </summary>
        public static Color unityFocusBlue { get { return Colorf.RGBToColor(0x3357d9,1f); } }
        #endregion

        #region byte channel -> Color
        /// <summary>
        /// Converts a set of 8bits channels into Color
        /// </summary>
        /// <param name="a">Alpha</param>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>        
        /// <returns>Color instance</returns>
        static public Color ARGBToColor(byte a, byte r, byte g, byte b) {
            float ca = ((float)a) * InvByte;
            float cr = ((float)r) * InvByte;
            float cg = ((float)g) * InvByte;
            float cb = ((float)b) * InvByte;
            return new Color(cr, cg, cb, ca);
        }

        /// <summary>
        /// Converts a set of 8bits channels into Color
        /// </summary>        
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>        
        /// <returns>Color instance</returns>
        static public Color RGBToColor(byte r, byte g, byte b) { return ARGBToColor(255,r,g,b); }
        #endregion

        #region uint -> Color
        /// <summary>
        /// Converts a 32 bit ARGB color to Color class.
        /// </summary>
        /// <param name="v">Color as 32bit number</param>
        /// <returns>Color instance</returns>
        static public Color ARGBToColor(uint v) { return ARGBToColor((byte)((v >> 24) & 0xff),(byte)((v >> 16) & 0xff), (byte)((v >> 8) & 0xff), (byte)(v & 0xff)); }

        /// <summary>
        /// Converts a 32 bit RGBA color to Color class.
        /// </summary>
        /// <param name="v">Color as 32bit number</param>
        /// <returns>Color instance</returns>
        static public Color RGBAToColor(uint v) { return ARGBToColor((byte)((v    ) & 0xff), (byte)((v >> 24) & 0xff), (byte)((v >> 16) & 0xff), (byte)((v >> 8) & 0xff)); }

        /// <summary>
        /// Converts a 24 bit RGB color to Color class, with the separate floating point alpha.
        /// </summary>
        /// <param name="v">Color as 24bit number</param>
        /// <param name="a">Alpha value in [0,1] range</param>
        /// <returns>Color instance</returns>
        static public Color RGBToColor(uint v,float a) { return ARGBToColor((byte)(Mathf.Clamp01(a)*255f), (byte)((v >> 16) & 0xff), (byte)((v >> 8) & 0xff), (byte)((v   ) & 0xff)); }

        /// <summary>
        /// Converts a 24 bit RGB color to Color class, with the separate floating point alpha.
        /// </summary>
        /// <param name="v">Color as 24bit number</param>
        /// <param name="a">Alpha value in [0,255] range</param>
        /// <returns>Color instance</returns>
        static public Color RGBToColor(uint v,byte a) { return ARGBToColor(a, (byte)((v >> 16) & 0xff), (byte)((v >> 8) & 0xff), (byte)((v   ) & 0xff)); }

        /// <summary>
        /// Converts a 24 bit RGB color to Color class, with the separate floating point alpha.
        /// </summary>
        /// <param name="v">Color as 24bit number</param>        
        /// <returns>Color instance</returns>
        static public Color RGBToColor(uint v) { return RGBToColor(v,255); }
        #endregion

        #region String -> Color
        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <param name="p_number_style"></param>
        /// <param name="p_default"></param>
        /// <returns></returns>
        static public Color ParseRGB(string p_v,System.Globalization.NumberStyles p_number_style,Color p_default) {
            uint v = 0;
            if(!uint.TryParse(p_v,p_number_style,null,out v)) return p_default;
            return RGBToColor(v);
        }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <param name="p_number_style"></param>
        /// <param name="p_default"></param>
        /// <returns></returns>
        static public Color ParseRGB(string p_v,System.Globalization.NumberStyles p_number_style) { return ParseRGB(p_v,p_number_style,transparent); }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <param name="p_default"></param>
        /// <returns></returns>
        static public Color ParseRGB(string p_v,Color p_default) { return ParseRGB(p_v,System.Globalization.NumberStyles.HexNumber,p_default); }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <returns></returns>
        static public Color ParseRGB(string p_v) { return ParseRGB(p_v,System.Globalization.NumberStyles.HexNumber,transparent); }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <param name="p_number_style"></param>
        /// <param name="p_default"></param>
        /// <returns></returns>
        static public Color ParseARGB(string p_v,System.Globalization.NumberStyles p_number_style,Color p_default) {
            uint v = 0;
            if(!uint.TryParse(p_v,p_number_style,null,out v)) return p_default;
            return ARGBToColor(v);
        }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <param name="p_number_style"></param>
        /// <param name="p_default"></param>
        /// <returns></returns>
        static public Color ParseARGB(string p_v,System.Globalization.NumberStyles p_number_style) { return ParseARGB(p_v,p_number_style,transparent); }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <param name="p_default"></param>
        /// <returns></returns>
        static public Color ParseARGB(string p_v,Color p_default) { return ParseARGB(p_v,System.Globalization.NumberStyles.HexNumber,p_default); }

        /// <summary>
        /// Parses the string into a RGB color
        /// </summary>
        /// <param name="p_v"></param>
        /// <returns></returns>
        static public Color ParseARGB(string p_v) { return ParseARGB(p_v,System.Globalization.NumberStyles.HexNumber,transparent); }        
        #endregion

        #region Gradient
        /// <summary>
        /// Interpolates a lsit of colors
        /// </summary>
        /// <param name="r"></param>
        /// <param name="p_colors"></param>
        /// <returns></returns>
        static public Color Gradient(float r,params Color[] p_colors) {
            float len = (float)p_colors.Length;
            float pos = r*(len-1f);
            int i0 = Mathf.FloorToInt(pos);
            int i1 = Mathf.CeilToInt(pos);
            if(i1>=p_colors.Length)i1 = p_colors.Length-1;
            float blend = pos - ((float)i0);
            return Color.Lerp(p_colors[i0],p_colors[i1],blend);
        }

        /// <summary>
        /// Interpolates a lsit of colors
        /// </summary>
        /// <param name="r"></param>
        /// <param name="p_colors"></param>
        /// <returns></returns>
        static public Color Gradient(float r,params uint[] p_colors) {
            float len = (float)p_colors.Length;
            float pos = r*(len-1f);
            int i0 = Mathf.FloorToInt(pos);
            int i1 = Mathf.CeilToInt(pos);
            if(i1>=p_colors.Length)i1 = p_colors.Length-1;
            float blend = pos - ((float)i0);
            Color c0 = ARGBToColor(p_colors[i0]);
            Color c1 = ARGBToColor(p_colors[i1]);
            return Color.Lerp(c0,c1,blend);
        }
        #endregion

        #region Operations
        /// <summary>
        /// Component wise add
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static public uint AddARGB(uint a,uint b) {
            int ca = (int)Mathf.Clamp(((a>>24)&0xff) + ((b>>24)&0xff),0,255);
            int cr = (int)Mathf.Clamp(((a>>16)&0xff) + ((b>>16)&0xff),0,255);
            int cg = (int)Mathf.Clamp(((a>> 8)&0xff) + ((b>> 8)&0xff),0,255);
            int cb = (int)Mathf.Clamp(((a    )&0xff) + ((b    )&0xff),0,255);
            return (uint)((ca<<24)|(cr<<16)|(cg<< 8)|(cb));
        }

        /// <summary>
        /// Component wise add
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static public uint AddRGBA(uint a,uint b) {
            int cr = (int)Mathf.Clamp(((a>>24)&0xff) + ((b>>24)&0xff),0,255);
            int cg = (int)Mathf.Clamp(((a>>16)&0xff) + ((b>>16)&0xff),0,255);
            int cb = (int)Mathf.Clamp(((a>> 8)&0xff) + ((b>> 8)&0xff),0,255);
            int ca = (int)Mathf.Clamp(((a    )&0xff) + ((b    )&0xff),0,255);
            return (uint)((cr<<24)|(cg<<16)|(cb<< 8)|(ca));
        }

        /// <summary>
        /// Component wise add
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static public uint AddRGB(uint a,uint b) {            
            int cr =(int)Mathf.Clamp(((a>>16)&0xff) + ((b>>16)&0xff),0,255);
            int cg =(int)Mathf.Clamp(((a>> 8)&0xff) + ((b>> 8)&0xff),0,255);
            int cb =(int)Mathf.Clamp(((a    )&0xff) + ((b    )&0xff),0,255);
            return (uint)((cr<<16)|(cg<<8)|(cb));
        }
        #endregion

        #region ImageProc
        /// <summary>
        /// Bilinear scale a list of pixels.
        /// </summary>
        /// <param name="p_image"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_out_width"></param>
        /// <param name="p_out_height"></param>
        /// <returns></returns>
        static public Color[] BilinearScale(Color[] p_image,int p_width,int p_height, int p_out_width,int p_out_height) {
            int w0 = p_width;
            int h0 = p_height;
            int w1 = p_out_width;
            int h1 = p_out_height;
            Color[] res = new Color[w1*h1];
            float iwm1 = w1 - 1;            
            float ihm1 = h1 - 1;
            iwm1 = iwm1 <= 0f ? 0f : (1f / iwm1);
            ihm1 = ihm1 <= 0f ? 0f : (1f / ihm1);
            for (int py=0;py<h1;py++) {
                float ry = ((float)py)*ihm1;
                for(int px=0;px<w1;px++) {
                    float rx = ((float)px)*iwm1;
                    int pos = px + (py*w1);
                    res[pos] = GetPixelBilinear(p_image,w0,h0,rx,ry);
                }
            }            
            return res;
        }

        /// <summary>
        /// Bilinear scale an image using threads.
        /// </summary>
        /// <param name="p_image"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_out_width"></param>
        /// <param name="p_out_height"></param>
        /// <param name="p_callback"></param>
        static public void BilinearScale(Color[] p_image,int p_width,int p_height,int p_out_width,int p_out_height,System.Action<Color[]> p_callback) {
            Thread thd = new Thread(delegate() {
                Color[] res = BilinearScale(p_image,p_width,p_height,p_out_width,p_out_height);                
                #if UNITY_EDITOR
                    Process.Start(delegate(ProcessContext ctx,Process pp) { if (pp.time < 1f / 60f) return true; if(p_callback!=null)p_callback(res); return false; }, ProcessContext.Editor);
                #else                    
                    Process.Start(delegate(ProcessContext ctx,Process pp) { if (pp.time < 1f / 60f) return true; if(p_callback!=null)p_callback(res); return false; }, ProcessContext.Update);
                #endif
            });
            thd.Start();
        }

        /// <summary>
        /// Bilinear scale a texture.
        /// </summary>
        /// <param name="p_tex"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_mipmap"></param>
        /// <returns></returns>
        static public Texture2D BilinearScale(Texture2D p_tex,int p_width,int p_height,bool p_mipmap=false) {
            Color[] pixels = p_tex.GetPixels();
            Color[] res    = BilinearScale(pixels,p_tex.width,p_tex.height,p_width,p_height);
            Texture2D output = new Texture2D(p_width,p_height, TextureFormat.ARGB32,p_mipmap);
            output.SetPixels(res);
            return output;
        }

        /// <summary>
        /// Async bilinear scale.
        /// </summary>
        /// <param name="p_tex"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_mipmap"></param>
        /// <param name="p_callback"></param>
        /// <returns></returns>
        static public Texture2D BilinearScale(Texture2D p_tex,int p_width,int p_height,System.Action<Texture2D> p_callback,bool p_mipmap=false) {
            Color[] pixels = p_tex.GetPixels();
            Texture2D output = new Texture2D(p_width,p_height, TextureFormat.ARGB32,p_mipmap);            
            BilinearScale(pixels,p_tex.width,p_tex.height,p_width,p_height,delegate(Color[] p_pixels) {                                
                output.SetPixels(p_pixels);
                output.Apply(true);
                if(p_callback!=null)p_callback(output);
            });
            return output;
        }

        /// <summary>
        /// Sample a bilinear pixel from an image as color array.
        /// </summary>
        /// <param name="p_image"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_uvx"></param>
        /// <param name="p_uvy"></param>
        /// <returns></returns>
        static public Color GetPixelBilinear(Color[] p_image,int p_width,int p_height,float p_uvx,float p_uvy) {
            int w = p_width;
            int h = p_height;
            float uvx = Mathf.Clamp01(p_uvx);
            float uvy = Mathf.Clamp01(p_uvy);
            float vx = uvx * ((float)w);
            float vy = uvy * ((float)h);
            float rx = vx - Mathf.Floor(vx);
            float ry = vy - Mathf.Floor(vy);
            int px = Mathf.FloorToInt(vx);
            int py = Mathf.FloorToInt(vy);            
            Color p0 = GetPixel(p_image,p_width,p_height,px,py);
            Color p1 = GetPixel(p_image,p_width,p_height,px+1,py);
            Color p2 = GetPixel(p_image,p_width,p_height,px,py+1);
            Color p3 = GetPixel(p_image,p_width,p_height,px+1,py+1);
            Color c0 = Color.Lerp(p0,p1,rx);
            Color c1 = Color.Lerp(p2,p3,rx);
            Color res = Color.Lerp(c0,c1,ry);
            return res;
        }

        /// <summary>
        /// Sample a nearest pixel from an image as color array.
        /// </summary>
        /// <param name="p_image"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_uvx"></param>
        /// <param name="p_uvy"></param>
        /// <returns></returns>
        static public Color GetPixelNearest(Color[] p_image,int p_width,int p_height,float p_uvx,float p_uvy) {            
            int w = p_width;
            int h = p_height;
            float uvx = Mathf.Clamp01(p_uvx);
            float uvy = Mathf.Clamp01(p_uvy);
            float vx = uvx * ((float)(w-1));
            float vy = uvy * ((float)(h-1));
            int px = Mathf.FloorToInt(vx);
            int py = Mathf.FloorToInt(vy);            
            Color p0 = GetPixel(p_image,p_width,p_height,px,py);
            return p0;            
        }

        /// <summary>
        /// Sample a pixel from an image as color array
        /// </summary>
        /// <param name="p_image"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_x"></param>
        /// <param name="p_y"></param>
        /// <returns></returns>
        static public Color GetPixel(Color[] p_image,int p_width,int p_height,int p_x,int p_y) {
            if(p_image==null)     return Color.clear;
            if(p_image.Length<=0) return Color.clear;
            int w = p_width;
            int h = p_height;
            int px = Mathf.Clamp(p_x,0,w-1);
            int py = Mathf.Clamp(p_y,0,h-1);            
            int pos = px + (py * w);
            pos = Mathf.Clamp(pos,0,p_image.Length-1);
            return p_image[pos];
        }

        /// <summary>
        /// Sets a pixel of a image as color array.
        /// </summary>
        /// <param name="p_image"></param>
        /// <param name="p_width"></param>
        /// <param name="p_height"></param>
        /// <param name="p_x"></param>
        /// <param name="p_y"></param>
        /// <param name="p_pixel"></param>
        static public void SetPixel(Color[] p_image,int p_width,int p_height,int p_x,int p_y,Color p_pixel) {
            if(p_image==null)     return;
            if(p_image.Length<=0) return;
            int w = p_width;
            int h = p_height;
            int px = Mathf.Clamp(p_x,0,w-1);
            int py = Mathf.Clamp(p_y,0,h-1);            
            int pos = px + (py * w);
            pos = Mathf.Clamp(pos,0,p_image.Length-1);
            p_image[pos] = p_pixel;
        }
        #endregion

        /// <summary>
        /// Given a color and a list of colors, try to find the index of the target inside the list.
        /// </summary>
        /// <param name="p_color"></param>
        /// <param name="p_list"></param>
        /// <param name="p_bias"></param>
        /// <returns></returns>
        static public int GetColorIndex(Color p_color,IList<Color> p_list,float p_bias=0.008f,bool p_clamp=true) {
            IList<Color> l = p_list;
            if(l.Count<=0) return -1;            
            int   min_idx  = 0;            
            float min_bias = GetBias(p_color,l[0]).magnitude;
            for(int i=1;i<l.Count;i++) {
                float bc = GetBias(p_color,l[i]).magnitude;
                if(bc < min_bias) {
                    min_idx  = i;                    
                    min_bias = bc;
                }
            }
            if(min_bias>p_bias) return -1;
            return min_idx;
        }

        /// <summary>
        /// Compares 2 colors and returns the diff bias.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="p_use_alpha"></param>
        /// <returns></returns>
        static public Vector4 GetBias(Color a,Color b,bool p_clamp=true) {
            Vector4 bias = new Vector4(0f,0f,0f,0f);
            if(p_clamp) {
                a.r = Mathf.Clamp01(a.r);
                a.g = Mathf.Clamp01(a.g);
                a.b = Mathf.Clamp01(a.b);
                a.a = Mathf.Clamp01(a.a);
                b.r = Mathf.Clamp01(b.r);
                b.g = Mathf.Clamp01(b.g);
                b.b = Mathf.Clamp01(b.b);
                b.a = Mathf.Clamp01(b.a);
            }
            bias.x = Mathf.Abs(a.r-b.r);
            bias.y = Mathf.Abs(a.g-b.g);
            bias.z = Mathf.Abs(a.b-b.b);
            bias.w = Mathf.Abs(a.a-b.a);
            return bias;
        }

    }

}