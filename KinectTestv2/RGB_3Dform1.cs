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

        OpenTK.GLControl glControl;  
     
        //boolean to store loaded state of form as loaded form state is not properly support by GLControl. - Doh
        public bool loaded = false;
        public bool render = true;
        public bool cameraSweepMode = false;


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
            glControl.Resize += glControl1_Resize;

            glControl.MouseMove += new MouseEventHandler(glControl_MouseMove);
            glControl.MouseDown += new MouseEventHandler(glControl_MouseDown);
            glControl.MouseUp += new MouseEventHandler(glControl_MouseUp);

        }

        private void glControl_Load(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("glControl_Load:  OpenGL version " + GL.GetString(StringName.Version));
            loaded = true;
            //setup openGL
            SetupViewport();
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("glControl1_Resize");
            //for some reason resize is launched prior to load - a known bug
            if (!loaded) return;
            SetupViewport();
        }

        private void SetupViewport()
        {

            //setup camera perspective
            double w = glControl.Width;
            double h = glControl.Height;

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            Matrix4d pers = Matrix4d.Perspective(1.22173048, w / h, 1, 1000);
            GL.LoadMatrix(ref pers);
            GL.Viewport(0, 0, (int)w, (int)h);

            // Setup parameters for Points
            //GL.PointSize(2f);
            GL.Enable(EnableCap.PointSmooth);
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);
          



        }



        public void Render()
        {
            System.Diagnostics.Debug.WriteLine("Render - " +  System.DateTime.Now);
            if (!loaded || !render ) return;

            
            Vector3d eye = new Vector3d(0.0, 0.0, -1);
            
            if (cameraSweepMode) {
                var timeSpan = 0.5 * (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                eye = new Vector3d(0.5 * Math.Sin(timeSpan),  0.1 * (1.0 + Math.Cos(timeSpan)), -1);
            }
            

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

            //adjust view
            GL.Rotate(angleY, 1f, 0, 0);
            GL.Rotate(-angleX, 0, 1f, 0);
            GL.Translate(0.0f, 0.0f, -1f);
            
            //address opacity - depending on allBodiesLookingAtCamera
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);

            //enable use of VBO
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);

            //push Vertex Buffer Object onto GPU
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_id);
            GL.VertexPointer(3, VertexPointerType.Float, Vector3.SizeInBytes, 0);

            //push Colour Buffer Object onto GPU
            GL.BindBuffer(BufferTarget.ArrayBuffer, cbo_id);
            GL.ColorPointer(4, ColorPointerType.Float, Vector4.SizeInBytes, 0);
               
            //draw GPU buffers
            GL.DrawArrays(BeginMode.Points, 0, vbo_size);

            //Wipe buffers - should be needed but GC does not work so manual deletion to avoid catastrophic memory leak
            GL.DeleteBuffer(vbo_id);
            GL.DeleteBuffer(cbo_id);

            //turn of VBO 
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);

            glControl.SwapBuffers();
            
        }

     
    
       



    
        public void updateVertices(Vector4[] colorarray, Vector3[] vertexarray) {

            //System.Diagnostics.Debug.WriteLine("updating vertices - vertex size = " + vertexarray.Length + ", colour size = " + colorarray.Length);

           
            vbo_size = vertexarray.Length;

            //load point cloud into a vertex buffer object
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
          

            render = true;

            //start sync render 
            Render();
        }

   
        private void glControl_MouseDown(object sender, EventArgs e)
        {
            MouseEventArgs ev = (e as MouseEventArgs);

            System.Diagnostics.Debug.WriteLine("glControl_MouseDown: " + ev.X + ", " + ev.Y);

           
            _mouseStartX = ev.X;
            _mouseStartY = ev.Y;
        }


        private void glControl_MouseUp(object sender, EventArgs e)
        {
            MouseEventArgs ev = (e as MouseEventArgs);

            System.Diagnostics.Debug.WriteLine("glControl_MouseUp: " + ev.X + ", " + ev.Y);

           
            angleXS = angleX;
            angleYS = angleY;
            distanceS = distance;
        }


        private void glControl_MouseMove(object sender, EventArgs e)
        {
            MouseEventArgs ev = (e as MouseEventArgs);
            System.Diagnostics.Debug.WriteLine("glControl_MouseMove: " + ev.X + ", " + ev.Y);

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
