using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KinectTestv2
{
    public partial class ColourForm : Form
    {
        
        public Body[] _bodies = null;
        Bitmap bitmap = null;

        private CoordinateMapper coordinateMapper = null;
        PictureBox pictureBox = new PictureBox();
        static int colorWidth = 1920;
        static int colorHeight = 1080;
        Brush brush = null;
        Pen pen = null;
        ColorSpacePoint point;
        Graphics g;

        public ColourForm(CoordinateMapper mapper)
        {
            InitializeComponent();
            g = pictureBox.CreateGraphics();
            coordinateMapper = mapper;

            pictureBox.Size = Size;
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBox_Paint);
            Resize += resize;
        }

        private void resize(object sender, EventArgs e)
        {
            pictureBox.Size = Size;
        }



       
        public void updatePicture(byte[] colourFrameData)
        {
            // Next get the frame's description and create an output bitmap image.
            bitmap = new Bitmap(colorWidth, colorHeight, System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            // Next, we create the raw data pointer for the bitmap, as well as the size of the image's data.
            BitmapData imageData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

            IntPtr imageDataPtr = imageData.Scan0;
            int size = imageData.Stride * bitmap.Height;
            Marshal.Copy(colourFrameData, 0, imageDataPtr, size);

            // Finally, unlock the output image's raw data again and create a new bitmap for the preview picture box.
            bitmap.UnlockBits(imageData);
            
            pictureBox.Image = bitmap;
           
        }

        private void pictureBox_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pictureBox_Paint - " + System.DateTime.Now);
            if(bitmap!=null) bitmap.Dispose();


            //  pictureBox.Image = bitmap;

            Graphics g = e.Graphics;

            if (_bodies != null)
            {
                for (int i = 0; i < _bodies.Count(); i++)
                {
                    Body body = _bodies[i];
                    if (body != null && body.IsTracked)
                    {

                        

                        switch (i)
                        {

                            case 1:
                                brush = Brushes.Yellow;
                                break;

                            case 2:
                                brush = Brushes.Green;
                                break;

                            case 3:
                                brush = Brushes.Blue;
                                break;

                            case 4:
                                brush = Brushes.Red;
                                break;

                            case 5:
                                brush = Brushes.Pink;
                                break;

                            default:
                                brush = Brushes.White;
                                break;
                        }

                        pen = new Pen(brush, 3);
                        pen.Alignment = PenAlignment.Center;

                        drawBody(brush, pen, g, body);
                    }
                }
            }
            
        }


        private void drawBody(Brush brush, Pen pen, Graphics g, Body body)
        {

            if (body.IsTracked)
            {
                /*drawJoint(brush, g, body.Joints[JointType.Head]);
                drawJoint(brush, g, body.Joints[JointType.Neck]);
                drawJoint(brush, g, body.Joints[JointType.SpineBase]);
                drawJoint(brush, g, body.Joints[JointType.SpineMid]);
                drawJoint(brush, g, body.Joints[JointType.ShoulderLeft]);
                drawJoint(brush, g, body.Joints[JointType.ElbowLeft]);
                drawJoint(brush, g, body.Joints[JointType.WristLeft]);
                drawJoint(brush, g, body.Joints[JointType.HandLeft]);
                drawJoint(brush, g, body.Joints[JointType.ShoulderRight]);
                drawJoint(brush, g, body.Joints[JointType.ElbowRight]);
                drawJoint(brush, g, body.Joints[JointType.WristRight]);
                drawJoint(brush, g, body.Joints[JointType.HandRight]);
                drawJoint(brush, g, body.Joints[JointType.HipLeft]);
                drawJoint(brush, g, body.Joints[JointType.KneeLeft]);
                drawJoint(brush, g, body.Joints[JointType.AnkleLeft]);
                drawJoint(brush, g, body.Joints[JointType.FootLeft]);
                drawJoint(brush, g, body.Joints[JointType.HipRight]);
                drawJoint(brush, g, body.Joints[JointType.KneeRight]);
                drawJoint(brush, g, body.Joints[JointType.AnkleRight]);
                drawJoint(brush, g, body.Joints[JointType.FootRight]);
                drawJoint(brush, g, body.Joints[JointType.SpineShoulder]);
                //drawJoint(g, body.Joints[JointType.HandTipLeft]);
                //drawJoint(g, body.Joints[JointType.ThumbLeft]);
                //drawJoint(g, body.Joints[JointType.HandTipRight]);
                //drawJoint(g, body.Joints[JointType.ThumbRight]);
                */
                //Torse
                drawBone(pen, g, body.Joints[JointType.Head], body.Joints[JointType.Neck]);
                drawBone(pen, g, body.Joints[JointType.Neck], body.Joints[JointType.SpineShoulder]);
                drawBone(pen, g, body.Joints[JointType.SpineShoulder], body.Joints[JointType.SpineMid]);
                drawBone(pen, g, body.Joints[JointType.SpineMid], body.Joints[JointType.SpineBase]);
                drawBone(pen, g, body.Joints[JointType.SpineMid], body.Joints[JointType.SpineBase]);
                drawBone(pen, g, body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderLeft]);
                drawBone(pen, g, body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderRight]);
                drawBone(pen, g, body.Joints[JointType.SpineBase], body.Joints[JointType.HipLeft]);
                drawBone(pen, g, body.Joints[JointType.SpineBase], body.Joints[JointType.HipRight]);

                //Left Arm
                drawBone(pen, g, body.Joints[JointType.ShoulderLeft], body.Joints[JointType.ElbowLeft]);
                drawBone(pen, g, body.Joints[JointType.ElbowLeft], body.Joints[JointType.WristLeft]);
                drawBone(pen, g, body.Joints[JointType.WristLeft], body.Joints[JointType.HandLeft]);
                //drawBone(g, body.Joints[JointType.HandLeft], body.Joints[JointType.HandTipLeft]);
                //drawBone(g, body.Joints[JointType.WristLeft], body.Joints[JointType.ThumbLeft]);

                //Right Arm
                drawBone(pen, g, body.Joints[JointType.ShoulderRight], body.Joints[JointType.ElbowRight]);
                drawBone(pen, g, body.Joints[JointType.ElbowRight], body.Joints[JointType.WristRight]);
                drawBone(pen, g, body.Joints[JointType.WristRight], body.Joints[JointType.HandRight]);
                //drawBone(g, body.Joints[JointType.HandRight], body.Joints[JointType.HandTipRight]);
                //drawBone(g, body.Joints[JointType.WristRight], body.Joints[JointType.ThumbRight]);

                //Left Leg
                drawBone(pen, g, body.Joints[JointType.HipLeft], body.Joints[JointType.KneeLeft]);
                drawBone(pen, g, body.Joints[JointType.KneeLeft], body.Joints[JointType.AnkleLeft]);
                drawBone(pen, g, body.Joints[JointType.AnkleLeft], body.Joints[JointType.FootLeft]);

                //Right Leg
                drawBone(pen, g, body.Joints[JointType.HipRight], body.Joints[JointType.KneeRight]);
                drawBone(pen, g, body.Joints[JointType.KneeRight], body.Joints[JointType.AnkleRight]);
                drawBone(pen, g, body.Joints[JointType.AnkleRight], body.Joints[JointType.FootRight]);
            }

        }

        private Point toColourSpace(CameraSpacePoint position)
        {

            point = coordinateMapper.MapCameraPointToColorSpace(position);
        
            if (point.X < 0 || point.Y < 0) return new Point();
            return new Point((int)(point.X / colorWidth * pictureBox.Width), (int)(point.Y / colorHeight * pictureBox.Height));
        }


        private void drawBone(Pen pen, Graphics g, Joint jointA, Joint jointB)
        {

            if (jointA.TrackingState == TrackingState.NotTracked || jointB.TrackingState == TrackingState.NotTracked) return;
            Point pointA = toColourSpace(jointA.Position);
            Point pointB = toColourSpace(jointB.Position);

            if (pointA.IsEmpty || pointB.IsEmpty) return;
            g.DrawLine(pen, pointA, pointB);
        }

        private void drawJoint(Brush brush, Graphics g, Joint joint)
        {

            if (joint.TrackingState == TrackingState.NotTracked) return;
            Point point = toColourSpace(joint.Position);
            if(!point.IsEmpty) g.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);

        }

       

       




    }
}
