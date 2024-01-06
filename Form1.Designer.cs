using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VectorMath
{
    partial class Form1
    {
        MegaBitmap.DirectBitmap backBuffer;
        bool runContinuous = true;
        byte[,] screenbuffer;

        int frame = 0;

        private System.ComponentModel.IContainer components = null;

        const int width = 320;
        const int height = 200;

        float[] sin32 = new float[256];
        float[] cos32 = new float[256];

        public class Vector
        {
            public float x, y, z;

            public Vector(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        public class Matrix
        {
            public float m11, m12, m13;
            public float m21, m22, m23;
            public float m31, m32, m33;

            public Matrix(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33)
            {
                this.m11 = m11;
                this.m12 = m12;
                this.m13 = m13;
                this.m21 = m21;
                this.m22 = m22;
                this.m23 = m23;
                this.m31 = m31;
                this.m32 = m32;
                this.m33 = m33;
            }
        }

        public Vector[] points = new Vector[]
        {
            new Vector( -1, -1,  1),
            new Vector(  1, -1,  1),
            new Vector(  1,  1,  1),
            new Vector( -1,  1,  1),
            new Vector( -1, -1, -1),
            new Vector(  1, -1, -1),
            new Vector(  1,  1, -1),
            new Vector( -1,  1, -1),
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        static string LittleEndianHex(UInt32 number)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            string retval = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                retval += "$" + bytes[i].ToString("X2");

                if(i != 3)
                    retval += ",";
            }

            return retval;
        }

        static string LittleEndian(UInt32 number)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            string retval = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                retval += bytes[i].ToString("X2");
            }

            return retval;
        }

        public float[] vertices;
        public int[] indices;
        public float[] normals;
        public int[] materials;

        public UInt32[] verticesXEx;
        public UInt32[] verticesYEx;
        public UInt32[] verticesZEx;

        public UInt32[] normalsXEx;
        public UInt32[] normalsYEx;
        public UInt32[] normalsZEx;

        public UInt32[] indicesPt1;
        public UInt32[] indicesPt2;
        public UInt32[] indicesPt3;

        public UInt32[] cols;

        public UInt32 F2I(float a)
        {
            UInt32 foo = (UInt32)(a * 256 * 256);
            return foo;
        }

        public UInt32 ConvertIndex(int a)
        {
            if (a < 0)
                return (UInt32)Math.Abs(a + 1);
            else
                return (UInt32)a;
        }


        public void ExtractForM65()
        {
            verticesXEx = new uint[vertices.Length / 3];
            verticesYEx = new uint[vertices.Length / 3];
            verticesZEx = new uint[vertices.Length / 3];

            normalsXEx = new uint[indices.Length / 3];
            normalsYEx = new uint[indices.Length / 3];
            normalsZEx = new uint[indices.Length / 3];

            indicesPt1 = new uint[indices.Length / 3];
            indicesPt2 = new uint[indices.Length / 3];
            indicesPt3 = new uint[indices.Length / 3];

            cols = new uint[indices.Length / 3];

            for (int i=0; i<vertices.Length / 3; i++)
            {
                verticesXEx[i] = F2I(vertices[i * 3 + 0]);
                verticesYEx[i] = F2I(vertices[i * 3 + 1]);
                verticesZEx[i] = F2I(vertices[i * 3 + 2]);
            }

            for (int i=0; i<indices.Length / 3; i++)
            {
                indicesPt1[i] = ConvertIndex(indices[i * 3 + 0]);
                indicesPt2[i] = ConvertIndex(indices[i * 3 + 1]);
                indicesPt3[i] = ConvertIndex(indices[i * 3 + 2]);
            }

            for (int i=0; i< indices.Length / 3; i++)
            {
                normalsXEx[i] = F2I(normals[i * 9 + 0]);
                normalsYEx[i] = F2I(normals[i * 9 + 1]);
                normalsZEx[i] = F2I(normals[i * 9 + 2]);
            }

            for(int i=0; i<materials.Length; i++)
            {
                cols[i] = (UInt32)(128 + (32 * materials[i]));
            }
        }

        public static string GetBetween(string strSource, string strStart)
        {
            int Start, End;

            string sub = "";

            Start = strSource.IndexOf(strStart, 0) + strStart.Length;
            End = strSource.IndexOf("{", Start);
            Start = strSource.IndexOf("a: ", Start);
            Start += 3;
            End = strSource.IndexOf("}", Start);
            End -= 2;
            sub += strSource.Substring(Start, End - Start);
            return sub;
        }

        public static int GetNumber(string strSource, string strStart)
        {
            int Start, End;

            Start = strSource.IndexOf(strStart, 0) + strStart.Length;
            End = strSource.IndexOf("{", Start);
            string sub = strSource.Substring(Start, End - Start);

            int.TryParse(sub, out int foo);

            return foo;
        }

        public int[] GetIntegers(string str, int num)
        {
            int[] numbers = new int[num];

            var split = str.Split(',');
            for(int i=0; i<num; i++)
            {
                numbers[i] = int.Parse(split[i]);
            }

            return numbers;
        }

        public float[] GetFloats(string str, int num)
        {
            float[] numbers = new float[num];

            var split = str.Split(',');
            for (int i = 0; i < num; i++)
            {
                numbers[i] = float.Parse(split[i]);
            }

            return numbers;
        }

        private void Init()
        {
            StreamReader sr = new StreamReader("untitled.fbx", ASCIIEncoding.UTF8 /* ASCIIEncoding.ASCII */);
            string filestring = sr.ReadToEnd();

            string tableTxt = "";

            backBuffer = new MegaBitmap.DirectBitmap(width, height, 2, 2);
            screenbuffer = new byte[width, height];

            tableTxt += "sin32" + Environment.NewLine;
            for (int i=0; i<256; i++)
            {
                var s = Math.Sin(2.0f * Math.PI * (float)i / 256);
                var s2 = (UInt32)(256 * 256 * s);
                sin32[i] = (float)s;

                if (i % 4 == 0)
                    tableTxt += Environment.NewLine + ".byte ";

                tableTxt += LittleEndian(s2);

                if (i % 4 != 3)
                    tableTxt += ", ";
            }

            tableTxt += Environment.NewLine;

            tableTxt += Environment.NewLine + "cos32" + Environment.NewLine;
            for (int i = 0; i < 256; i++)
            {
                var s = Math.Cos(2.0f * Math.PI * (float)i / 256);
                var s2 = (UInt32)(256 * 256 * s);
                cos32[i] = (float)s;

                if (i % 4 == 0)
                    tableTxt += Environment.NewLine + ".byte ";

                tableTxt += LittleEndian(s2);

                if (i % 4 != 3)
                    tableTxt += ", ";
            }

            /*
            for (int i = 0; i < 256; i++)
            {
                var s = Math.Sin(2.0f * Math.PI * (float)i / 256);
                var s2 = (byte)(128 * s);

                if (i % 16 == 0)
                    tableTxt += Environment.NewLine;

                tableTxt += "$" + s2.ToString("X2");

                if(i % 16 != 15)
                    tableTxt += ", ";
            }
            */

            textBox1.Text = tableTxt;

            int numVertices = GetNumber(filestring, "Vertices: *");
            string vrt = GetBetween(filestring, "Vertices: *");
            vertices = GetFloats(vrt, numVertices);

            int numIndices = GetNumber(filestring, "PolygonVertexIndex: *");
            string idx = GetBetween(filestring, "PolygonVertexIndex: *");
            indices = GetIntegers(idx, numIndices);

            int numNormals = GetNumber(filestring, "Normals: *");
            string nrm = GetBetween(filestring, "Normals: *");
            normals = GetFloats(nrm, numNormals);

            int numMaterials = GetNumber(filestring, "Materials: *");
            string mat = GetBetween(filestring, "Materials: *");
            materials = GetIntegers(mat, numMaterials);

            ExtractForM65();

            /*
            string final =
                numVertices.ToString() + Environment.NewLine +
                vrt + Environment.NewLine + Environment.NewLine +
                numIndices.ToString() + Environment.NewLine +
                idx + Environment.NewLine + Environment.NewLine +
                numNormals.ToString() + Environment.NewLine +
                nrm + Environment.NewLine;
            */

            string final = "";

            final += ".align 256" + Environment.NewLine + "vertsx" + Environment.NewLine;
            for(int i=0; i<verticesXEx.Length; i++)
            {
                final += "\t.byte " + LittleEndianHex(verticesXEx[i]) + Environment.NewLine;
            }
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "vertsy" + Environment.NewLine;
            for (int i = 0; i < verticesYEx.Length; i++)
            {
                final += "\t.byte " + LittleEndianHex(verticesYEx[i]) + Environment.NewLine;
            }
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "vertsz" + Environment.NewLine;
            for (int i = 0; i < verticesZEx.Length; i++)
            {
                final += "\t.byte " + LittleEndianHex(verticesZEx[i]) + Environment.NewLine;
            }
            final += Environment.NewLine;
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "normalsx" + Environment.NewLine;
            for (int i = 0; i < normalsXEx.Length; i++)
            {
                final += "\t.byte " + LittleEndianHex(normalsXEx[i]) + Environment.NewLine;
            }
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "normalsy" + Environment.NewLine;
            for (int i = 0; i < normalsYEx.Length; i++)
            {
                final += "\t.byte " + LittleEndianHex(normalsYEx[i]) + Environment.NewLine;
            }
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "normalsz" + Environment.NewLine;
            for (int i = 0; i < normalsZEx.Length; i++)
            {
                final += "\t.byte " + LittleEndianHex(normalsZEx[i]) + Environment.NewLine;
            }
            final += Environment.NewLine;
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "indicesp1" + Environment.NewLine;
            for (int i = 0; i < indicesPt1.Length; i++)
            {
                final += "\t.byte " + indicesPt1[i] + "*4" + Environment.NewLine;
            }
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "indicesp2" + Environment.NewLine;
            for (int i = 0; i < indicesPt2.Length; i++)
            {
                final += "\t.byte " + indicesPt2[i] + "*4" + Environment.NewLine;
            }
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "indicesp3" + Environment.NewLine;
            for (int i = 0; i < indicesPt3.Length; i++)
            {
                final += "\t.byte " + indicesPt3[i] + "*4" + Environment.NewLine;
            }
            final += Environment.NewLine;
            final += Environment.NewLine;

            final += ".align 256" + Environment.NewLine + "orgcol" + Environment.NewLine;
            for (int i = 0; i < cols.Length; i++)
            {
                final += "\t.byte $" + cols[i].ToString("X2") + Environment.NewLine;
            }
            final += Environment.NewLine;

            textBox1.Text = final;
        }

        private void ClearFrame()
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    screenbuffer[x, y] = 32;
        }

        private void DoWork()
        {
            int rotX = frame;
            int rotY = frame;
            int rotZ = frame;

            float sx = sin32[rotX % 256];
            float sy = sin32[rotY % 256];
            float sz = sin32[rotZ % 256];

            float cx = cos32[rotX % 256];
            float cy = cos32[rotY % 256];
            float cz = cos32[rotZ % 256];

            /*
                Matrix mX = new Matrix(
                        1,   0,   0,
                        0,  cx, -sx,
                        0,  sx,  cx
                );
                Matrix mY = new Matrix(
                        cy,   0,  sy,
                        0,   1,   0,
                    -sy,   0,  cy
                );
                Matrix mZ = new Matrix(
                        cz, -sz,   0,
                        sz,  cz,   0,
                        0,   0,   1
                );

                Matrix mXYZ = new Matrix(
                    cx*cy, cx*sy*sz-sx*cz, cx*sy*cz+sx*sz,
                    sx*cy, sx*sy*sz+cx*cz, sx*sy*cz-cx*sz,
                        -sy,    cy*sz,          cy*cz
                );
            */

            // need 6 temp regs!

            // MATH_MULA sz
            // MATH_MULB sx, t1
            // MATH_MULB cx, t2
            // MATH_MULB sy, t3
            // MATH_MULB cy, m32
            // MATH_MULA cz
            // MATH_MULB cy, m33
            // MATH_MULB sx, t4
            // MATH_MULB cx, t5
            // MATH_NEG cx, m31
            // ldq cx
            // stq MULTINB
            // ldq MULTOUT
            // stq t6
            // MATH_MULA sx
            // MATH_MULB cy, m21
            // MATH_MULADDB t3, t5, m22
            // MATH_MULSUBB t6, t2, m23
            // MATH_MULA cx
            // MATH_MULB cy, m11
            // MATH_MULSUBB t3, t4, m12
            // MATH_MULADDB t6, t1, m13

            // multa = sz
            // multb = sx
            var t1 = sx*sz;
            // multb = cx
            var t2 = cx*sz;
            // multb = sy
            var t3 = sy*sz;
            // multb = cy
            var m32 = cy*sz;

            // multa = cz
            var m33 = cy*cz;
            // multb = sx
            var t4 = sx*cz;
            // multb = cx
            var t5 = cx*cz;
            // multb = sy
            var m31 = -sy;
            var t6 = sy*cz;

            // multa = sx
            // multb = cy
            var m21 = sx*cy;
            // multb = t3
            var m22 = sx*t3+t5;
            // multb = t6
            var m23 = sx*t6-t2;

            // multa = cx
            // multb = cy
            var m11 = cx*cy;
            // multb = t3
            var m12 = cx*t3-t4;
            // multb = t6
            var m13 = cx*t6+t1;

            Matrix mXYZ = new Matrix(
                m11, m12, m13,
                m21, m22, m23,
                m31, m32, m33
            );


            // cos(A + B) = cos(A)cos(B) - sin(A)sin(B)
            // sin(A + B) = cos(A)sin(B) + sin(A)cos(B)

            // cos(-a) = cos(a)
            // sin(-a) = -sin(a)

            // sin(a)sin(b) = (cos(a - b) - cos(a + b)) / 2
            // cos(a)cos(b) = (cos(a + b) + cos(a - b)) / 2
            // sin(a)cos(b) = (sin(a + b) + sin(a - b)) / 2

            // MATRIX THAT USES NO MULTIPLICATIONS:

            //     [A B C]
            // M = [D E F]
            //     [G H I]

            // Where
            // A = (cos(t1) + cos(t2)) / 2
            // B = (sin(t1) - sin(t2)) / 2
            // C = sin(sy)
            // D = (sin(t3) - sin(t4)) / 2 + (cos(t6) - cos(t5) + cos(t8) - cos(t7)) / 4
            // E = (cos(t3) + cos(t4)) / 2 + (sin(t5) - sin(t6) - sin(t7) - sin(t8)) / 4
            // F = (sin(t9) - sin(t10)) / 2
            // G = (cos(t4) - cos(t3)) / 2 + (sin(t6) - sin(t5) - sin(t8) - sin(t7)) / 4
            // H = (sin(t3) + sin(t4)) / 2 + (cos(t6) - cos(t5) + cos(t7) - cos(t8)) / 4
            // I = (cos(t9) + cos(t10)) / 2

            // with
            // t1 = sy - sz
            // t2 = sy + sz
            // t3 = sx + sz
            // t4 = sx - sz
            // t5 = sx + sy + sz = sx + t2
            // t6 = sx - sy + sz = sx - t1
            // t7 = sx + sy - sz = sx + t1
            // t8 = sy + sz - sx = t2 - sx
            // t9 = sy - sx
            // t10 = sy + sx

            //DebugInit();

            //DebugVar("sx", sx);
            //DebugVar("sy", sy);
            //DebugVar("sz", sz);
            //DebugVar("cx", cx);
            //DebugVar("cy", cy);
            //DebugVar("cz", cz);

            //DebugNewLine();

            //DebugVar("t1", t1);
            //DebugVar("t2", t2);
            //DebugVar("t3", t3);
            //DebugVar("t4", t4);
            //DebugVar("t5", t5);
            //DebugVar("t6", t6);

            //DebugNewLine();
            //DebugMatrix(mXYZ);

            for (int foo = 0; foo < points.Length; foo++)
            {
                Vector p = points[foo];
                Vector p2 = new Vector(p.x, p.y, p.z);

                // rotate around X
                //p2.x = p.x * mX.m11 + p.y * mX.m12 + p.z * mX.m13;
                //p2.y = p.x * mX.m21 + p.y * mX.m22 + p.z * mX.m23;
                //p2.z = p.x * mX.m31 + p.y * mX.m32 + p.z * mX.m33;

                //p2.x = p.x;
                //p2.y = p.y * cx + p.z * -sx;
                //p2.z = p.y * sx + p.z *  cx;
                //p = new Vector(p2.x, p2.y, p2.z);

                // rotate around Y
                //p2.x = p.x * mY.m11 + p.y * mY.m12 + p.z * mY.m13;
                //p2.y = p.x * mY.m21 + p.y * mY.m22 + p.z * mY.m23;
                //p2.z = p.x * mY.m31 + p.y * mY.m32 + p.z * mY.m33;

                //p2.x = p.x * cy + p.z * sy;
                //p2.y = p.y;
                //p2.z = p.x * -sy + p.z * cy;
                //p = new Vector(p2.x, p2.y, p2.z);

                // rotate around Z
                //p2.x = p.x * mZ.m11 + p.y * mZ.m12 + p.z * mZ.m13;
                //p2.y = p.x * mZ.m21 + p.y * mZ.m22 + p.z * mZ.m23;
                //p2.z = p.x * mZ.m31 + p.y * mZ.m32 + p.z * mZ.m33;

                //p2.x = p.x * cz + p.y * -sz;
                //p2.y = p.x * sz + p.y * cz;
                //p2.z = p.z;
                //p = new Vector(p2.x, p2.y, p2.z);


                // rotate around X, Y and Z
                p2.x = p.x * mXYZ.m11 + p.y * mXYZ.m12 + p.z * mXYZ.m13;
                p2.y = p.x * mXYZ.m21 + p.y * mXYZ.m22 + p.z * mXYZ.m23;
                p2.z = p.x * mXYZ.m31 + p.y * mXYZ.m32 + p.z * mXYZ.m33;

                DebugVar("p.x * mXYZ.m11", p.x * mXYZ.m11);
                DebugVar("p.y * mXYZ.m12", p.y * mXYZ.m12);
                DebugVar("p.z * mXYZ.m13", p.z * mXYZ.m13);


                p = new Vector(p2.x, p2.y, p2.z);

                DebugVar("p.x", p.x);
                DebugVar("p.y", p.y);
                DebugVar("p.z", p.z);

                DebugVar("-1", -1);

                float distance = 8;
                float z = 400.0f / (distance - p.z);

                // apply projection matrix
                p2.x = p.x * z;
                p2.y = p.y * z;
                p2.z = 0;

                p = new Vector(p2.x, p2.y, p2.z);


                screenbuffer[160 + (int)p.x, 100 + (int)p.y] = 255;
            }

            //DebugDone();

            frame++;
        }

        string debugTxt = "";

        private void DebugInit()
        {
            debugTxt = "";
        }

        private void DebugDone()
        {
            textBox1.Text = debugTxt;
        }

        private void DebugVar(string vn, float v)
        {
            UInt32 v2 = (UInt32)(v * 256 * 256);
            debugTxt += v.ToString() + " " + vn + " = " + LittleEndian(v2) + Environment.NewLine;
        }

        private void DebugNewLine()
        {
            debugTxt += Environment.NewLine;
        }

        private void DebugMatrix(Matrix m)
        {
            UInt32 fpm11 = (UInt32)(m.m11 * 256 * 256);
            UInt32 fpm12 = (UInt32)(m.m12 * 256 * 256);
            UInt32 fpm13 = (UInt32)(m.m13 * 256 * 256);
            UInt32 fpm21 = (UInt32)(m.m21 * 256 * 256);
            UInt32 fpm22 = (UInt32)(m.m22 * 256 * 256);
            UInt32 fpm23 = (UInt32)(m.m23 * 256 * 256);
            UInt32 fpm31 = (UInt32)(m.m31 * 256 * 256);
            UInt32 fpm32 = (UInt32)(m.m32 * 256 * 256);
            UInt32 fpm33 = (UInt32)(m.m33 * 256 * 256);

            debugTxt += LittleEndian(fpm11) + " ";
            debugTxt += LittleEndian(fpm12) + " ";
            debugTxt += LittleEndian(fpm13) + " ";
            debugTxt += Environment.NewLine;
            debugTxt += LittleEndian(fpm21) + " ";
            debugTxt += LittleEndian(fpm22) + " ";
            debugTxt += LittleEndian(fpm23) + " ";
            debugTxt += Environment.NewLine;
            debugTxt += LittleEndian(fpm31) + " ";
            debugTxt += LittleEndian(fpm32) + " ";
            debugTxt += LittleEndian(fpm33) + " ";

            debugTxt += Environment.NewLine;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            runContinuous = false;
            ClearFrame();
            DoWork();
            this.pictureBox1.Refresh();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            backBuffer.FromArray(screenbuffer);
            // backBuffer.DrawGrid(8, 8);
            e.Graphics.DrawImage(backBuffer.Bitmap, 0, 0, backBuffer.Width, backBuffer.Height);
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (runContinuous)
            {
                ClearFrame();
                DoWork();
                pictureBox1.Refresh();
            }
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            runContinuous = !runContinuous;
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.button2 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Location = new System.Drawing.Point(7, 9);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(642, 402);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBox1_Paint);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(7, 419);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(89, 25);
            this.button1.TabIndex = 1;
            this.button1.Text = "1 frame";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // textBox1
            // 
            this.textBox1.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.Location = new System.Drawing.Point(655, 9);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBox1.Size = new System.Drawing.Size(534, 402);
            this.textBox1.TabIndex = 2;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 16;
            this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(102, 419);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(95, 25);
            this.button2.TabIndex = 3;
            this.button2.Text = "Continuous";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.Button2_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.ClientSize = new System.Drawing.Size(1195, 451);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.pictureBox1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button button1;
        private TextBox textBox1;
        private Timer timer1;
        private Button button2;
    }
}

