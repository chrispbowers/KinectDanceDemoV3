using OpenTK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;

namespace KinectTestv2
{
    public partial class RGB_3Dform1 : Form
    {

        int vbo_id, cbo_id;
        int vbo_size;

        public bool allBodiesLookingAtCamera = false;
        float fadeState = 1.0f;

        OpenTK.GLControl glControl;
        //frame sizes
        static int depthWidth = 512;
        static int depthHeight = 424;
        static int bodyWidth = 512;
        static int bodyHeight = 424;
        static int colorWidth = 1920;
        static int colorHeight = 1080;

        //boolean to store loaded state of form as loaded form state is not properly support by GLControl. - Doh
        public bool loaded = false;
        public bool rendered = false;
        //model display and HID states
        private int _mouseStartX = 0;
        private int _mouseStartY = 0;
        private float angleX = 0;
        private float angleY = 0;
        private float angleXS = 0;
        private float angleYS = 0;
        private float distance = 5;
        private float distanceS = 5;
        private float rotSpeed = 0.1f;



        public RGB_3Dform1()
        {
            InitializeComponent();

            glControl = new OpenTK.GLControl();
            glControl.Dock = DockStyle.Fill;
            Controls.Add(glControl);

            glControl.Load += glControl_Load;
            glControl.Paint += glControl_Paint;
            glControl.Resize += glControl1_Resize;
            
        }



        private void glControl_Load(object sender, EventArgs e)
        {

            loaded = true;

            //setup openGL
            GL.ClearColor(1, 1, 1, 0);
           
            SetupViewport();

        }


        private void glControl_Paint(object sender, PaintEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("glControl_Paint");
            Render();
        }

        public void Render()
        {
            System.Diagnostics.Debug.WriteLine("Render - " + System.DateTime.Now);
            if (loaded && rendered)
            {

                glControl.MakeCurrent();

                //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                Vector3d eye = new Vector3d(0.0, 0.0, -1);
                Vector3d target = new Vector3d(0.0, 0.0, 0.0);
                Vector3d up = new Vector3d(0.0, 1.0, 0.0);
                Matrix4d lookAt = Matrix4d.LookAt(eye, target, up);

                //setup camera view
                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadIdentity();
                GL.LoadMatrix(ref lookAt);

                //setup openGL
                GL.ClearColor(0.5f, 0.5f, 0.5f, 0.5f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                GL.Translate(0.0f, 0.0f, 2.0f);

                GL.Rotate(angleY, 1.0f, 0, 0);
                GL.Rotate(angleX, 0, 1.0f, 0);

                if (allBodiesLookingAtCamera && fadeState > 0.0f)
                    fadeState -= 0.1f;
                else if (fadeState < 1.0f) fadeState += 0.1f;


                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                GL.Enable(EnableCap.Blend);

                /*
                GL.Begin(BeginMode.Points);
                for (int i = 0; i < depthWidth * depthHeight; i++)
                {

                    //GL.Color4(colorarray[i * 3 + 0], colorarray[i * 3 + 1], colorarray[i * 3 + 2], fadeState);
                    //GL.Vertex3(vertexarray[i * 3 + 0], vertexarray[i * 3 + 1], vertexarray[i * 3 + 2]);
                }

                GL.End();*/

                GL.Begin(BeginMode.Points);

                GL.EnableClientState(ArrayCap.VertexArray);
                GL.EnableClientState(ArrayCap.ColorArray);

                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_id);
        
                GL.VertexPointer(3, VertexPointerType.Float, Vector3.SizeInBytes, 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, cbo_id);
                GL.ColorPointer(4, ColorPointerType.Float, Vector4.SizeInBytes, 0);
           

                GL.DrawArrays(BeginMode.Points, 0, vbo_size);
                GL.End();
                //GL.DisableClientState(ArrayCap.VertexArray);
                //GL.DisableClientState(ArrayCap.ColorArray);


                glControl.SwapBuffers();


                

            }
            
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!loaded) return;
            SetupViewport();
            glControl.Invalidate();
          
          
        }


        private void SetupViewport()
        {

            //setup camera perspective
            double w = glControl.Width;
            double h = glControl.Height;

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            Matrix4d pers = Matrix4d.Perspective(70, w / h, 1, 1000);
            GL.LoadMatrix(ref pers);
            GL.Viewport(0, 0, (int)w, (int)h);

        }



    


            public void updateVertices(Vector4[] colorarray, Vector3[] vertexarray) {

            System.Diagnostics.Debug.WriteLine("updating vertices - vertex size = " + vertexarray.Length + ", colour size = " + colorarray.Length);

           
            //load point cloud into a vertex buffer object

            vbo_size = vertexarray.Length; // Necessary for rendering later on

            GL.GenBuffers(1, out vbo_id);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_id);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          new IntPtr(vertexarray.Length * BlittableValueType.StrideOf(vertexarray)),
                          vertexarray, BufferUsageHint.StreamDraw);

            GL.GenBuffers(1, out cbo_id);
            GL.BindBuffer(BufferTarget.ArrayBuffer, cbo_id);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          new IntPtr(colorarray.Length * BlittableValueType.StrideOf(colorarray)),
                          colorarray, BufferUsageHint.StreamDraw);
          

            rendered = true;

            Render();
           // glControl.Invalidate();
           // glControl.Update();
        }

   
        private void glControl_MouseDown(object sender, EventArgs e)
        {
            MouseEventArgs ev = (e as MouseEventArgs);
            _mouseStartX = ev.X;
            _mouseStartY = ev.Y;
        }


        private void glControl_MouseUp(object sender, EventArgs e)
        {
            MouseEventArgs ev = (e as MouseEventArgs);
            angleXS = angleX;
            angleYS = angleY;
            distanceS = distance;
        }


        private void glControl_MouseMove(object sender, EventArgs e)
        {
            MouseEventArgs ev = (e as MouseEventArgs);
            if (ev.Button == MouseButtons.Left)
            {
                angleX = angleXS + (ev.X - _mouseStartX) * rotSpeed;
                angleY = angleYS + (ev.Y - _mouseStartY) * rotSpeed;
            }
            if (ev.Button == MouseButtons.Right)
            {
                distance = Math.Max(2.9f, distanceS + (ev.Y - _mouseStartY) / 10.0f);
            }
        }
    }
}
