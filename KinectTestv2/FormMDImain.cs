using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Kinect;
using OpenTK;
using Microsoft.Kinect.Face;
using Gma.System.MouseKeyHook;
using System.Windows.Input;

namespace KinectTestv2
{
    public partial class FormMDImain : Form
    {



        //maximumFrameGap between face frames (low numbers effect stabilty and framerate of cloud point render - high numbers decrease framerate of face detection)
        int maxFaceFrameGap = 5;

        //how many face frames should the face propery exists before we act
        int eyeWindowSize = 2;

        //frame sizes
        static int depthWidth = 512;
        static int depthHeight = 424;
        static int bodyWidth = 512;
        static int bodyHeight = 424;
        static int colorWidth = 1920;
        static int colorHeight = 1080;

        static int EyesOpen = 0;
        static int EyesWink = 1;
        static int EyesClosed = 2;

        bool colorFrameProcessed = false;
        bool depthFrameProcessed = false;
        bool bodyFrameProcessed = false;
        bool bodyIndexFrameProcessed = false;
        int faceFrameGapCounter = 0;


        //windows form components
        RGB_3Dform1 processedCloud;
        ColourForm colourForm;

        //demo states
        int bodyCount;
        int indexBodyCount = 0;
        bool fadeEnabled = false;
        bool displayRaw = true;
        bool fadeStarted = false;
        bool bottomEffect = true;
        float fadeState = 1.0f;
        static float fadeOutRate = 0.9f;
        static int fadeHeldLength = 100;
        static float fadeInRate = 1.1f;
        int fadeIndex = 0;

       

        //user face expression states
        int[] winkCount;
        int[] eyesClosedCount;

        

        int[] eyes;

       


        // Store kinekt frames and data
        ColorSpacePoint[] colorPoints = new ColorSpacePoint[depthWidth * depthHeight];    // Maps depth pixels to rgb pixels
        CameraSpacePoint[] cameraPoints = new CameraSpacePoint[depthWidth * depthHeight];     // Maps depth pixels to 3d coordinates
        CoordinateMapper _mapper;
        FaceFrameSource[] faceFrameSources;
        FaceFrameReader[] faceFrameReaders;
        FaceFrameResult[] faceFrameResults;
        FaceFrameResult[] prevFaceFrameResults;
        ushort[] depthFrameData = new ushort[depthWidth * depthHeight];
        byte[] colourFrameData = new byte[colorWidth * colorHeight * 4];
        byte[] bodyIndexFrameData = new byte[bodyWidth * bodyHeight];


        Dictionary<Vector3, OpenTK.Vector4> RGBpointCloud = new Dictionary<Vector3, OpenTK.Vector4>(); 

        //store array of tracked bodies
        Body[] bodies = new Body[6];

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
                eyesClosedCount = new int[bodyCount];
                winkCount = new int[bodyCount];
                eyes = new int[bodyCount];
               

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
                }

                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameArrived;

                _sensor.Open();

            }

            processedCloud = new RGB_3Dform1();
            colourForm = new ColourForm(_mapper);

            //processedCloud.MdiParent = this;
            //colourForm.MdiParent = this;

            processedCloud.Show();
            colourForm.Show();

        }


        private void FormMDImain_Load(object sender, EventArgs e)
        {

        }

   

        private void MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("MultiSourceFrameArrived - " + faceFrameGapCounter + " - " + System.DateTime.Now);

            //if paused then don't handle main loop
            if (Keyboard.GetKeyStates(Key.P) == KeyStates.Toggled) return;
            displayRaw = Keyboard.GetKeyStates(Key.R) != KeyStates.Toggled;
            bottomEffect = Keyboard.GetKeyStates(Key.B) != KeyStates.Toggled;
            fadeEnabled = Keyboard.GetKeyStates(Key.F) == KeyStates.Toggled;
            if (Keyboard.GetKeyStates(Key.Z) == KeyStates.Down) fadeStarted = true;
            processedCloud.cameraSweepMode = Keyboard.GetKeyStates(Key.C) == KeyStates.Toggled;
            
            
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            colorFrameProcessed = false;
            depthFrameProcessed = false;
            bodyFrameProcessed = false;
            bodyIndexFrameProcessed = false;


            if (multiSourceFrame == null) return;

            using (BodyFrame bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    indexBodyCount = 0;
                    bodyFrame.GetAndRefreshBodyData(bodies);

                    // iterate through each face source
                    for (int i = 0; i < this.bodyCount; i++)
                    {
                        if (bodies[i].IsTracked)
                        {
                            indexBodyCount++;
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
                    colourFrame.CopyConvertedFrameDataToArray(colourFrameData, ColorImageFormat.Bgra);
                    colorFrameProcessed = true;
                }
            }


            using (BodyIndexFrame bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame())
            {
                if (bodyIndexFrame != null)
                {
                    //bodyIndexFrameData = new byte[bodyWidth * bodyHeight];
                    bodyIndexFrame.CopyFrameDataToArray(bodyIndexFrameData);
                    bodyIndexFrameProcessed = true;
                }
            }

            for(int i=0; i<bodyCount; i++)
            {
                using (FaceFrame faceFrame = faceFrameReaders[i].AcquireLatestFrame()) {

                    if (faceFrame == null) break;

                    faceFrameGapCounter = 0;

                    // check if this face frame has valid face frame results
                    if (faceFrame.FaceFrameResult != null)
                    {
                    
                        //copy old faceframe result to prev array
                        prevFaceFrameResults[i] = faceFrameResults[i];

                        // store this face frame result to use later
                        faceFrameResults[i] = faceFrame.FaceFrameResult;

                    }

                    else
                    {
                        // indicates that the latest face frame result from this reader is invalid
                        faceFrameResults[i] = null;
                    }
                
                }
            }
           

            // update draw states if new data
            if (bodyFrameProcessed) colourForm._bodies = bodies;
            if (colorFrameProcessed) colourForm.updatePicture(colourFrameData);
            if (faceFrameGapCounter == 0) faceState();
            if (depthFrameProcessed && colorFrameProcessed && bodyIndexFrameProcessed && faceFrameGapCounter < maxFaceFrameGap) CreateVertices();

            //iterate facegap counter to maintain count of number of frames since last processing a face
            faceFrameGapCounter++;

        }


        public void faceState()
        {
            //System.Diagnostics.Debug.WriteLine("faceState - " + System.DateTime.Now);
   
            int trackedBodyCount = 0;


            for (int i = 0; i < bodyCount; i++)
            {
                if (prevFaceFrameResults[i] != null && faceFrameResults[i] != null)
                {

                    //eyes closed  or wink
                    if (faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed]  
                        &&
                        faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed])
                        eyesClosedCount[i]++;
                    else eyesClosedCount[i] = 0;
                    if (eyesClosedCount[i] >= eyeWindowSize)
                    {
                        if (faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == DetectionResult.Yes 
                            && 
                            faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == DetectionResult.Yes) eyes[i] = EyesClosed;

                        else if (faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] != DetectionResult.Yes
                            &&
                            faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] != DetectionResult.Yes) eyes[i] = EyesOpen;

                        else eyes[i] = EyesWink;
                        
                    }
                   

                }

                if (eyes[i] == EyesWink && fadeEnabled) {
                    fadeStarted = true;
                    System.Diagnostics.Debug.Write("WINKER!!!!");
                }

                
                    //System.Diagnostics.Debug.Write("UserID=" + i + " eyes = " + eyes[i]);
                    //System.Diagnostics.Debug.WriteLine("");
                
               
            }
            
           
            if (fadeStarted)
            {
                if (fadeIndex == 0 && fadeState > 0.01f) fadeState = Math.Max(0.0f, fadeState * fadeOutRate);  //fade in
                else if (fadeState == 1.0f && fadeIndex >= fadeHeldLength)
                {
                    fadeIndex = 0; fadeStarted = false;
                } //fade reset
                else if (fadeIndex >= fadeHeldLength) fadeState = Math.Min(1.0f, fadeState * fadeInRate);     //fade out
               
                else fadeIndex++; //fade hold

            }
                 
            //System.Diagnostics.Debug.WriteLine("Fade state " + fadeState);



        }


        //Creates vertices from frames for rendering to openGL
        public void CreateVertices()
        {

            int highestPersonFound = -1;

            RGBpointCloud = new Dictionary<Vector3, OpenTK.Vector4>();


            _mapper.MapDepthFrameToCameraSpace(depthFrameData, cameraPoints);
            _mapper.MapDepthFrameToColorSpace(depthFrameData, colorPoints);


            for (int i = 0; i < cameraPoints.Length; i++)
            {

                //check colour point is within the colour bounds
                ColorSpacePoint p = colorPoints[i];
                if (p.X < 0 || p.Y < 0 || p.X > colorWidth || p.Y > colorHeight) continue;

                int idx = (int)p.X + colorWidth * (int)p.Y;

                

                //if filtering data account for following exceptions
                if (!displayRaw)
                {
                    //if background index (ie not allocated to a tracked body
                    if (bodyIndexFrameData[i] == 0xff) continue;

                    //dont display people with their eyes shut
                    if (eyes[bodyIndexFrameData[i]] == EyesClosed) continue;

                    //the bottom effect
                    if (indexBodyCount >= 2)
                    {



                        if (highestPersonFound < 0) highestPersonFound = bodyIndexFrameData[i];

                        if (highestPersonFound == bodyIndexFrameData[i])
                        {
                            if (bottomEffect)
                            {
                                RGBpointCloud.Add(
                                    new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                                    new OpenTK.Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                                continue;
                            }

                        }


                    }

                    //if a wink has been detected
                    if (fadeStarted)
                    {
                        //and the current point does not belong to a winker
                        if (eyes[bodyIndexFrameData[i]] != EyesWink)
                        {
                            //display cloud point with a fadestate 
                            RGBpointCloud.Add(
                                new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                                new OpenTK.Vector4(colourFrameData[4 * idx + 2] / 255f, colourFrameData[4 * idx + 1] / 255f, colourFrameData[4 * idx + 0] / 255f, fadeState));

                            //otherwise behave normally
                            continue;
                        }
                    }
                }
                
                //default case - populate  vertices with RGB colour 
                RGBpointCloud.Add(
                    new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                    new OpenTK.Vector4(colourFrameData[4 * idx + 2] / 255f, colourFrameData[4 * idx + 1] / 255f, colourFrameData[4 * idx + 0] / 255f, 1.0f));
           }

            processedCloud.updateVertices(RGBpointCloud.Values.ToArray<OpenTK.Vector4>(), RGBpointCloud.Keys.ToArray<Vector3>());
            
        }

        
        
    }
}
