using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Kinect;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Drawing.Imaging;
using OpenTK.Graphics;
using OpenTK;
using Microsoft.Kinect.Face;
using System.IO;

namespace KinectTestv2
{
    public partial class FormMDImain : Form
    {
        //frame sizes
        static int depthWidth = 512;
        static int depthHeight = 424;
        static int bodyWidth = 512;
        static int bodyHeight = 424;
        static int colorWidth = 1920;
        static int colorHeight = 1080;


        bool colorFrameProcessed = false;
        bool depthFrameProcessed = false;
        bool bodyFrameProcessed = false;
        bool bodyIndexFrameProcessed = false;
        bool faceFrameProcessed = false;


        Bitmap outputImage = null;

        //windows form components
        RGB_3Dform1 rawCloud;
        RGB_3Dform1 processedCloud;
        ColourForm colourForm;


        Graphics graphics;

        //demo states
       
        int firstSeenbodyIndex = -1;
       
        int bodyCount;

        //user face expression states
        int[] mouthOpenCount;
        int[] eyesClosedCount;
        int[] lookingAtSensorCount;
        bool[] mouthOpen;
        bool[] eyesClosed;
        bool[] lookingAtSensor;
        int faceBufferSize = 4;


      

        // Store kinekt frames and data
        ColorSpacePoint[] colorPoints = new ColorSpacePoint[depthWidth * depthHeight];    // Maps depth pixels to rgb pixels
        CameraSpacePoint[] cameraPoints =  new CameraSpacePoint[depthWidth * depthHeight];     // Maps depth pixels to 3d coordinates
        CoordinateMapper _mapper;
        FaceFrameSource[] faceFrameSources;
        FaceFrameReader[] faceFrameReaders;
        FaceFrameResult[] faceFrameResults;
        FaceFrameResult[] prevFaceFrameResults;
        ushort[] depthFrameData = new ushort[depthWidth * depthHeight];
        byte[] colourFrameData = new byte[colorWidth * colorHeight * 4];
        byte[] bodyIndexFrameData = new byte[bodyWidth * bodyHeight];


        Vector3[] rawvertexarray = new Vector3[depthWidth * depthHeight];
        OpenTK.Vector4[] rawcolorarray = new OpenTK.Vector4[depthWidth * depthHeight];

        float[] colorarray = null;
        float[] vertexarray = null;

       



        //store array of tracked bodies
        Body[] bodies = null;

        KinectSensor _sensor;
        




      

        public FormMDImain()
        {
            InitializeComponent();

          
            //get instance of sensor - only one sensor allowed
            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {
                
                _mapper = _sensor.CoordinateMapper;

                MultiSourceFrameReader multiSourceFrameReader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);

                bodyCount = _sensor.BodyFrameSource.BodyCount;

                //user face expression states counter
                mouthOpenCount = new int[bodyCount];
                eyesClosedCount = new int[bodyCount];
                lookingAtSensorCount = new int[bodyCount];
                mouthOpen = new bool[bodyCount];
                eyesClosed = new bool[bodyCount];
                lookingAtSensor = new bool[bodyCount];

                bodies = new Body[bodyCount];

                FaceFrameFeatures faceFeatures = FaceFrameFeatures.BoundingBoxInColorSpace |
                                                 FaceFrameFeatures.FaceEngagement |
                                                 FaceFrameFeatures.Glasses |
                                                 FaceFrameFeatures.Happy |
                                                 FaceFrameFeatures.LeftEyeClosed |
                                                 FaceFrameFeatures.MouthOpen |
                                                 FaceFrameFeatures.PointsInColorSpace |
                                                 FaceFrameFeatures.RightEyeClosed;


                faceFrameSources = new FaceFrameSource[bodyCount];
                faceFrameReaders = new FaceFrameReader[bodyCount];
                faceFrameResults = new FaceFrameResult[bodyCount];
                prevFaceFrameResults = new FaceFrameResult[bodyCount];

                for (int i = 0; i < bodyCount; i++)
                {
                    faceFrameSources[i] = new FaceFrameSource(_sensor, 0, faceFeatures);
                    faceFrameReaders[i] = faceFrameSources[i].OpenReader();
                   // faceFrameReaders[i].FrameArrived += FaceReaderFrameArrived;
                }

                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameArrived;

               


                _sensor.Open();



            }



            rawCloud = new RGB_3Dform1();
            processedCloud = new RGB_3Dform1();
            colourForm = new ColourForm(_mapper);



            //rawCloud.MdiParent = this;
            //processedCloud.MdiParent = this;
            //colourForm.MdiParent = this;

            rawCloud.Show();
            processedCloud.Show();
            colourForm.Show();

   
          

            

            

            //glControl1.MouseMove += new MouseEventHandler(glControl_MouseMove);
            //glControl1.MouseDown += new MouseEventHandler(glControl_MouseDown);
            //glControl1.MouseUp += new MouseEventHandler(glControl_MouseUp);

            
     

        }


       

       


        private void MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("MultiSourceFrameArrived - " + System.DateTime.Now);
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

             colorFrameProcessed = false;
             depthFrameProcessed = false;
             bodyFrameProcessed = false;
             bodyIndexFrameProcessed = false;

            


            if (multiSourceFrame == null)
                return;

            using (BodyFrame bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                int trackedBodyCount = 0;
                if (bodyFrame != null)
                {
                    // update body data
                    bodyFrame.GetAndRefreshBodyData(bodies);

                    // iterate through each face source
                    for (int i = 0; i < this.bodyCount; i++)
                    {
                        if (bodies[i].IsTracked)
                        {
                            trackedBodyCount++;
                            // check if a valid face is tracked in this face source
                            if (!faceFrameSources[i].IsTrackingIdValid)
                            {
                                // update the face frame source to track this body
                                faceFrameSources[i].TrackingId = bodies[i].TrackingId;
                            }
                        }                      
                    }
                    bodyFrameProcessed = true;

                   
                }
                

            }
           
            using (DepthFrame depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            {

                if (depthFrame != null)
                {
                    depthFrameData = new ushort[depthWidth * depthHeight];
                    depthFrame.CopyFrameDataToArray(depthFrameData);
                    depthFrameProcessed = true;
                }
            }

            using (ColorFrame colourFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            {
                if (colourFrame != null)
                {

                    //colourFrameData = new byte[colorWidth * colorHeight * 4];
                    colourFrame.CopyConvertedFrameDataToArray(colourFrameData, ColorImageFormat.Bgra);




                   
                                      
                    colorFrameProcessed = true;
                    
                }
            }

            
            using (BodyIndexFrame bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame())
            {
                if (bodyIndexFrame != null)
                {

                    bodyIndexFrameData = new byte[bodyWidth * bodyHeight];
                    bodyIndexFrame.CopyFrameDataToArray(bodyIndexFrameData);
                    bodyIndexFrameProcessed = true;
                }
            }
            

            if(bodyFrameProcessed) colourForm._bodies = bodies;
            if (colorFrameProcessed) colourForm.updatePicture(colourFrameData);

            
            
            if (depthFrameProcessed && colorFrameProcessed && bodyIndexFrameProcessed)// && faceFrameProcessed)
            {
                CreateVertices();
                
                faceFrameProcessed = false;

            }
            
    
        }


        public void faceState()
        {
           
            bool allBodiesLookingAtCamera = true;
            int trackedBodyCount = 0;

            for (int i = 0; i < bodyCount; i++)
            {
                if (prevFaceFrameResults[i] != null && faceFrameResults[i] != null)
                {

                    
                    //mouth open
                    if (faceFrameResults[i].FaceProperties[FaceProperty.MouthOpen] == prevFaceFrameResults[i].FaceProperties[FaceProperty.MouthOpen]) mouthOpenCount[i]++;
                    else mouthOpenCount[i] = 0;
                    if (mouthOpenCount[i] >= faceBufferSize) mouthOpen[i] = faceFrameResults[i].FaceProperties[FaceProperty.MouthOpen] == DetectionResult.Yes;

                    //eyes closed
                    if (faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed]
                        &&
                        faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed])
                        eyesClosedCount[i]++;
                    else eyesClosedCount[i] = 0;
                    if (eyesClosedCount[i] >= faceBufferSize)
                    {
                        eyesClosed[i] = faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == DetectionResult.Yes || faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == DetectionResult.Yes;

                        
                    }
                    
                    //looking at camera
                    if (faceFrameResults[i].FaceProperties[FaceProperty.Engaged] == prevFaceFrameResults[i].FaceProperties[FaceProperty.Engaged]) lookingAtSensorCount[i]++;
                    else lookingAtSensorCount[i] = 0;
                    if (lookingAtSensorCount[i] > faceBufferSize)
                    {
                        lookingAtSensor[i] = faceFrameResults[i].FaceProperties[FaceProperty.Engaged] == DetectionResult.Yes;

                    }

                    //check to see if all bodies are looking at the camera
                    trackedBodyCount++;
                    if (!lookingAtSensor[i]) allBodiesLookingAtCamera = false;

                }
            }

            if (trackedBodyCount == 0) allBodiesLookingAtCamera = false;

            processedCloud.allBodiesLookingAtCamera = allBodiesLookingAtCamera;


            //if (allBodiesLookingAtCamera) System.Diagnostics.Debug.WriteLine("all looking");

            faceFrameProcessed = true;
        }


        //Creates vertices from frames for rendering to openGL
        public void CreateVertices() {

            colorarray = new float[depthWidth * depthHeight * 3];
            vertexarray = new float[depthWidth * depthHeight * 3];


            rawvertexarray = new Vector3[depthWidth * depthHeight];
            rawcolorarray = new OpenTK.Vector4[depthWidth * depthHeight];

            _mapper.MapDepthFrameToCameraSpace(depthFrameData, cameraPoints);
            _mapper.MapDepthFrameToColorSpace(depthFrameData, colorPoints);

            
            for (int i = 0; i < cameraPoints.Length; i++)
            {

                //check colour point is with the colour bounds
                ColorSpacePoint p = colorPoints[i];
                if (p.X < 0 || p.Y < 0 || p.X > colorWidth || p.Y > colorHeight) continue;

                //populate raw vertices
                rawvertexarray[i] = new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z);

                //populate raw colours
                int idx = (int)p.X + colorWidth * (int)p.Y;
                rawcolorarray[i] = new OpenTK.Vector4(colourFrameData[4 * idx + 0] / 255f, colourFrameData[4 * idx + 1] / 255f, colourFrameData[4 * idx + 2] / 255f, colourFrameData[4 * idx + 3] / 255f);

               

                /*
                //if this is the first pixel from a tracked body log the index of the tracked body
                if (firstSeenbodyIndex == -1 && bodyIndexFrameData[i] != 0xff) firstSeenbodyIndex = bodyIndexFrameData[i];
   

                // add vertices of tracked bodies who do not have eyes closed
                if (bodyIndexFrameData[i] != 0xff && !eyesClosed[bodyIndexFrameData[i]])
                {
                    vertexarray[3 * i + 0] = cameraPoints[i].X;
                    vertexarray[3 * i + 1] = cameraPoints[i].Y;
                    vertexarray[3 * i + 2] = cameraPoints[i].Z;
                }
                
            

               

                //check if pixel in bound of colour
               
                {
                   
                }

                // add colour frame pixel colour to render array
                else
                {
                    int idx = (int)p.X + colorWidth * (int)p.Y;

                    rawcolorarray[3 * i + 0] = colourFrameData[4 * idx + 0] / 255f;
                    rawcolorarray[3 * i + 1] = colourFrameData[4 * idx + 1] / 255f;
                    rawcolorarray[3 * i + 2] = colourFrameData[4 * idx + 2] / 255f;


                    if (bodyIndexFrameData[i] != 0xff)
                    {
                            
                        //if first found person then display in coloured cloud point
                        if (bodyIndexFrameData[i] == firstSeenbodyIndex)
                        {

                            colorarray[3 * i + 0] = colourFrameData[4 * idx + 0] / 255f;
                            colorarray[3 * i + 1] = colourFrameData[4 * idx + 1] / 255f;
                            colorarray[3 * i + 2] = colourFrameData[4 * idx + 2] / 255f;
                        }

                        //all other should be shadow (black) - using a gray background
                        else
                        {
                            colorarray[3 * i + 0] = 0.0f;
                            colorarray[3 * i + 1] = 0.0f;
                            colorarray[3 * i + 2] = 0.0f;
                        }
                        
                    }
                }*/
                
            }

            
            rawCloud.updateVertices(rawcolorarray, rawvertexarray);
            //processedCloud.updateVertices(rawcolorarray, rawvertexarray);
            
        }



        private void FaceReaderFrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("FaceReaderFrameArrived");

            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {

                if (faceFrame != null)
                {
                    // get the index of the face source from the face source array
                    int index = -1;
                    for (int i = 0; i < bodyCount; i++)
                    {
                        if (this.faceFrameSources[i] == faceFrame.FaceFrameSource)
                        {
                            index = i;
                            break;
                        }
                    }

                    // check if this face frame has valid face frame results
                    if (faceFrame.FaceFrameResult != null)
                    {
                        //copy old faceframe result to prev array
                        prevFaceFrameResults[index] = faceFrameResults[index];

                        // store this face frame result to use later
                        faceFrameResults[index] = faceFrame.FaceFrameResult;
                        
                    }

                    else
                    {
                        // indicates that the latest face frame result from this reader is invalid
                        this.faceFrameResults[index] = null;
                    }
                }
            }

            //calculate state of faces in scene
            faceState();


        }



    
      


      


       


       

        private void FormMDImain_Load(object sender, EventArgs e)
        {

        }
    }
}
