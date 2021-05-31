using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Basler.Pylon;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using EasyModbus;
using System.Threading;
using SimpleTCP;
using QRCoder;
using System.Windows.Input;

namespace AnodePin
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        ModbusClient iomodule;
        Camera usbcam;

        bool extTrig = false;
        bool sftTrig = true;
        bool draw = false;
        bool btnrun;
        PixelDataConverter converter = new PixelDataConverter();
        Mat original = new Mat();
        int process = 0;
        int i;
        Mat imgInput = new Mat();
        Mat img = new Mat();
        Mat Sourceimg = new Mat();
        Mat tempa = new Mat();
        //Mat tempb = new Mat();
        Mat tempaa = new Mat();
        // Mat tempaaa = new Mat();
        Mat calib = new Mat();
        string[] storepf = new string[12];
        string[] storepf1 = new string[12];
        OpenCvSharp.Rect ta;
        OpenCvSharp.Rect rectfirst;
        OpenCvSharp.Rect rectsecond;
        int[] Xa = new int[24];
        int[] Ya = new int[24];
        int[] Wa = new int[24];
        int[] Ha = new int[24];
        int Anode_terminal;
        int Anode_terminal1;
        int contours = 0;
        int contours1 = 0;
        OpenCvSharp.Point LocationXY;
        OpenCvSharp.Point LocationX1Y1;
        bool IsMouseDown = false;
        Rectangle rect;
        int tx, ty, th, tw;
        OpenCvSharp.Rect t;
        Rect Rect_crop;
        Rect nk;
        Rect roi;
        bool finalpassfail=true;
        Rect rect2 = new Rect();
        Rect rect22 = new Rect();
        int j = 0;
        int temp;
        bool img_save = false;
        int count = 0;
        int failcount = 0;
        int passcount = 0;
        int totalcount = 0;
        int totalfail = 0;
        int totalpass = 0;
        string foldername = DateTime.Now.ToString("yyyy-MM-dd");
        string filename;

        SimpleTcpClient client;
        private void basler_gigi_load(object sender, EventArgs e)
        {
            timer2.Enabled = true;
            string licsense = DateTime.Now.ToString("yyyy-MM-dd");
            //if (licsense == "2020-03-04")
            //{
            //    Form1.ActiveForm.Close();
            //    throw new Exception("Licsense Expired");                          
            //}
            client = new SimpleTcpClient(); //Decalres a client for communication
            client.StringEncoder = Encoding.UTF8; //Character encoding is done for transmission
            client.DataReceived += Client_DataReceived; //Goes to the function for data/message transfer
            this.KeyPreview = true;
            qrcode.Select(); //qrcode textbox is activated initially
            try
            {
                log.Info("started..");
                //load_settings();                
                List<ICameraInfo> allCameras = CameraFinder.Enumerate(); // Ask the camera finder for a list of camera devices
                if (allCameras.Count == 0) //Counts the number of cameras
                {
                    toolStripProgressBar1.Value = 100;
                    throw new Exception("No devices found."); //Occurs if no cameras are connected
                }

                usbcam = new Camera(23130024.ToString()); //Connects to the camera with the serial number 2310024        
                iomodule = new ModbusClient("COM3"); //Used for PLC connection and detects the port where it is connected
                iomodule.Parity = System.IO.Ports.Parity.Even; //Sets the parity
                iomodule.Connect(); //Connects to the PLC
                usbcam.Open(); //Opens the camera
                toolStripProgressBar1.Value = 20;
                string oldPixelFormat = usbcam.Parameters[PLCamera.PixelFormat].GetValue(); // Remember the current pixel format.
                Console.WriteLine("Old PixelFormat  : {0} ({1})", usbcam.Parameters[PLCamera.PixelFormat].GetValue(), oldPixelFormat);
                //isAvail = Pylon.DeviceFeatureIsAvailable(hDev, "EnumEntry_PixelFormat_Mono8");
                Console.WriteLine("count  : {0}", usbcam.Parameters[PLCamera.AcquisitionMode].GetValue());
                if (!usbcam.Parameters[PLCamera.PixelFormat].TrySetValue(PLCamera.PixelFormat.Mono8)) //Checks the type of camera connected 
                {
                    /* Feature is not available. */
                    throw new Exception("Device doesn't support the MONO8 pixel format."); //Throws this excpetion if the camera connected is not of Mono8 format
                }
                //tlStrpPrgsBar1.Value = 50;
                usbcam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.Off);
                if (extTrig)
                {
                    usbcam.Parameters[PLCamera.TriggerSource].TrySetValue(PLCamera.TriggerSource.Line1); //Sets the trigger source to Line 1
                    usbcam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.On); //Turns the trigger mode on
                }
                usbcam.Parameters[PLCamera.ExposureTime].TrySetValue(10000);
                usbcam.StreamGrabber.ImageGrabbed += OnImageGrabbed; //Goes to the function OnImageGrabbed
                usbcam.StreamGrabber.GrabStopped += OnGrabStopped; //Goes to the function OnGrabStopped
                toolStripProgressBar1.Value = 100;
                toolStripStatusLabel1.Text = "Camera connected.."; //Indicates that the camera is connected
                toolStripStatusLabel1.Image = global::AnodePin.Properties.Resources.ok;
                if (sftTrig | extTrig)
                    btn_start.Enabled = true;
                //timer1.Enabled = true;
                //timer2.Enabled = true;
                //load_settings();    
            }
            catch (Exception ex)
            {
                toolStripProgressBar1.Value = 100;
                toolStripStatusLabel1.Image = global::AnodePin.Properties.Resources.error;
                toolStripStatusLabel1.Text = "Error..... " + ex.StackTrace;
                /* Retrieve the error message. */
                toolStripStatusLabel1.Text = "Exception caught:";
                toolStripStatusLabel1.Text += ex.Message;
                if (ex.Message != " ")
                {
                    //tlStrpStsLbl1.Text += "Last error message:" + ex.Message;
                    //tlStrpStsLbl1.Text += msg;
                }
                try
                {
                    // Close the camera.
                    if (usbcam != null)
                    {
                        usbcam.Close(); //Closes the camera
                        usbcam.Dispose(); //Releases the camera resources
                    }
                }
                catch (Exception)
                {
                    //No further handling here.
                }
            }
        }

        private void Client_DataReceived(object sender, SimpleTCP.Message e)
        {
            //This is used for TCP/IP Communication
            txtStatus.Invoke((MethodInvoker)delegate ()
            {
                txtStatus.Text += e.MessageString; //Message typed is shown in the text box
            });
        }

        private void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            draw = false;
            if (InvokeRequired)
            {
                /*If called from a different thread, we must use the Invoke method to marshal the call to the proper GUI thread.
                The grab result will be disposed after the event call. Clone the event arguments for marshaling to the GUI thread.*/
                BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed), sender, e.Clone());
                return;
            }
            Thread.Sleep(100);
            toolStripProgressBar1.Value = 20;
            Mat img, grayimg = new Mat();
            if (btnrun == true)
            {
                IGrabResult res = e.GrabResult;
                if (!res.IsValid)
                {
                    //Timeout occurred
                }
               
                if (res.GrabSucceeded)//Check to see if the image was grabbed successfully
                {
                    //Success. Perform image processing
                    Bitmap bitmap = new Bitmap(res.Width, res.Height, PixelFormat.Format32bppRgb); //Specifies that the format is 32 bits per pixel                   
                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat); //Lock the bits of the bitmap                    
                    converter.OutputPixelFormat = PixelType.BGRA8packed;
                    IntPtr ptrBmp = bmpData.Scan0; //Place the pointer to the buffer of the bitmap
                    converter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, res); //Exception handling to do
                    bitmap.UnlockBits(bmpData);
                    img = BitmapConverter.ToMat(bitmap);
                    img.CopyTo(original); //Assign a temporary variable to dispose the bitmap after assigning the new bitmap to the display control
                    //if (img_save)
                    //    Cv2.ImWrite(folderBrowserDialog2.SelectedPath + "\\" + i.ToString() + ".bmp", img);

                    Bitmap bitmapOld = img_disp.Image as Bitmap;
                    img_disp.Image = bitmap;
                    img_disp.SizeMode = PictureBoxSizeMode.Zoom;
                    if (bitmapOld != null)
                    {                      
                        bitmapOld.Dispose(); //Dispose the bitmap
                    }
                    process_image(img);
                    usbcam.StreamGrabber.Stop();
                }
                else if (!res.GrabSucceeded)
                {
                    //SetText(String.Format("Frame {0} wasn't grabbed successfully.  Error code = {1}\r\n", i + 1, res.ErrorCode));
                }
                ++i;
            }
            toolStripProgressBar1.Value = 100;
            toolStripStatusLabel1.Text = String.Format("Frame {0} grabbed.", i + 1);            
            e.DisposeGrabResultIfClone(); //Dispose the grab result if needed for returning it to the grab loop
        }

        private void OnGrabStopped(Object sender, GrabStopEventArgs e)
        {
            if (InvokeRequired)
            {
                //If called from a different thread, we must use the Invoke method to marshal the call to the proper thread
                BeginInvoke(new EventHandler<GrabStopEventArgs>(OnGrabStopped), sender, e);
                return;
            }
            btnrun = false;
            btn_stop.Enabled = true;
            btn_start.Enabled = true;
        }

        private void basler_gigi_closing(object sender, FormClosingEventArgs e)
        {
            DialogResult dg = MessageBox.Show("Do you want to Close the Solution?", "Closing", MessageBoxButtons.YesNo); //Displays a warning before closing asking for confirmation
            if (dg == DialogResult.Yes)
            {
                if (usbcam != null)
                {
                    usbcam.Close();
                    usbcam.Dispose(); //If yes, closes and frees the resources of the camera
                }
                e.Cancel = false;
            }
            else if (dg == DialogResult.No)
            {
                e.Cancel = true; //Won't close
            }
        }

        private void btn_browse_Click(object sender, EventArgs e)
        {
            //If clicked, images can be displayed on picture box for testing (for offline images)
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                img_disp.Image = new Bitmap(openFileDialog1.FileName);
                img_disp.SizeMode = PictureBoxSizeMode.StretchImage;
                textBox1.Text = openFileDialog1.FileName;
                toolStripProgressBar1.Value = 50;
                imgInput = new Mat(openFileDialog1.FileName);
            }
            else
            {
                textBox1.Text = "No picture selected";
                MessageBox.Show("Nothing", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btn_test_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            richTextBox2.Clear();
            richTextBox3.Clear();
            richTextBox4.Clear();
            //Calibration is done for distance checking
            img = new Mat(openFileDialog1.FileName);
            Mat dst = new Mat(); 
            Mat CameraMatrix = new Mat(3, 3, MatType.CV_64F); //A camera matrix is set

            double[] mat_cam = new double[] { 53381.69208946366, 0, 1569.9721291898586, 0, 52954.12275455012, 1092.28124208259261, 0, 0, 1 }; //Array of calibrated values are considered into an array
            Mat cammat = new Mat(3, 3, MatType.CV_64FC1, mat_cam);

            var distcoffs = new double[] { -3.3513508576414153, -1.810048688050843, -0.045023126601117486, -0.018985015109092312, -0.0011433546611971516 }; //Values for distance co-efficeients is taken after getting the calibration results
            Mat dist = new Mat(1, 4, MatType.CV_64FC1, distcoffs);

            OpenCvSharp.Size sz = new OpenCvSharp.Size(2464, 2056); //Image width and height are chosen
            OpenCvSharp.Size sz1 = new OpenCvSharp.Size(2464, 2056); //Image width and height are chosen
            OpenCvSharp.Rect kk;
            Mat newcammat = Cv2.GetOptimalNewCameraMatrix(cammat, dist, sz, 1, sz1, out kk); //Outs a newcamera matrix based on free scaling parameters
            Cv2.Undistort(img, dst, cammat, dist, newcammat); //Corrects lens distortions
            Cv2.CvtColor(dst, dst, ColorConversionCodes.BGR2GRAY);
            calib = new Mat(dst, kk); //The final image is given to a new variable
    
            process_image(calib); //Image Processing starts       
        }

        private void load_settings()
        {
            //Helps to create new folders if not already created
            if(!Directory.Exists(@"F:\Vision_Results\"))
            {
                Directory.CreateDirectory(@"F:\Vision_Results\"); //If there is no "Vision_Results", it'll be created
            }
            if (!Directory.Exists(@"F:\Vision_Results\Failed_Images\"))
            {
                Directory.CreateDirectory(@"F:\Vision_Results\Failed_Images\"); //Similarly, in the above folder, if there's no "Failed_Images", it'll be created
            }
            if (!Directory.Exists(@"F:\Vision_Results\Failed_Images" + foldername))
            {
                Directory.CreateDirectory(@"F:\Failed_Images" + foldername); //It'll create a final folder based on the date
            }
            if (!Directory.Exists(@"F:\Vision_Results\Passed_Images\"))
            {
                Directory.CreateDirectory(@"F:\Vision_Results\Passed_Images\"); //If there's no "Failed_Images" in "Vision_Results", it'll be created
            }
            if (!Directory.Exists(@"F:\Vision_Results\Passed_Images" + foldername))
            {
                Directory.CreateDirectory(@"F:\Vision_Results\Passed_Images\" + foldername); //It'll create a final folder based on the date
            }

            filename = string.Format(@"F:\{0:yyyy-MM-dd}.csv", DateTime.Now); //Defining a .csv file 

            if (!File.Exists(filename))
            {
                File.WriteAllText(filename, totalcount.ToString() + "\t" + passcount.ToString() + "t" + failcount.ToString()); //For opening first time in a day
            }
            else 
            {
                var secondonwards = File.ReadAllLines(filename).Count();
                File.WriteAllText(filename, totalcount.ToString() + "\t" + passcount.ToString() + "t" + failcount.ToString()); //For opening second and so on in a day
            }
        }

        private void process_image(Mat imgIn, bool offline = false)
        {
            double[] array1 = new double[1000];
            double[] array11 = new double[1000];
            double[] array2 = new double[1000];
            double[] array3 = new double[1000];
            double[] array22 = new double[1000]; 
            double[] array33 = new double[1000];
            //Mat dst = new Mat();
            //Mat CameraMatrix = new Mat(3, 3, MatType.CV_64F); //A new Camera Matrix is defined

            //double[] mat_cam = new double[] { 53381.69208946366, 0, 1569.9721291898586, 0, 52954.12275455012, 1092.28124208259261, 0, 0, 1 }; //Array of calibrated values are considered into an array
            //Mat cammat = new Mat(3, 3, MatType.CV_64FC1, mat_cam);

            //var distcoffs = new double[] { -3.3513508576414153, -1.810048688050843, -0.045023126601117486, -0.018985015109092312, -0.0011433546611971516 }; //Values for distance co-efficeients is taken after getting the calibration results
            //Mat dist = new Mat(1, 4, MatType.CV_64FC1, distcoffs);

            //OpenCvSharp.Size sz = new OpenCvSharp.Size(2464, 2056); //Image width and height are chosen
            //OpenCvSharp.Size sz1 = new OpenCvSharp.Size(2464, 2056); //Image width and height are chosen
            //OpenCvSharp.Rect kk;
            //Mat newcammat = Cv2.GetOptimalNewCameraMatrix(cammat, dist, sz, 1, sz1, out kk); //Outs a newcamera matrix based on free scaling parameters
            //Cv2.Undistort(imgIn, dst, cammat, dist, newcammat); //Corrects lens distortions
            //Cv2.CvtColor(dst, dst, ColorConversionCodes.BGR2GRAY);
            //calib = new Mat(dst, kk); //The final image is given to a new variable
            //calib.CopyTo(imgInput); //The calibrated image is given to a new matrix for image processing

            imgIn.CopyTo(imgInput); //If the image is taken from offline, it is copied to a new variable
            bool midstore = true;
            rectfirst = new Rect(405, 896, 1559, 140); //Mentions the dimensions of the ROI for the first row of pins
            rectsecond = new Rect(405, 1096, 1559, 140); //Mentions the dimensions of the ROI for the second row of pins
            Bitmap bmp22 = BitmapConverter.ToBitmap(imgInput); 
            img_disp.Image = bmp22;
            img_disp.SizeMode = PictureBoxSizeMode.StretchImage;

            tempa = new Mat(imgInput, rectfirst); //The first row cropped portion is given to a new variable
            tempaa = new Mat(imgInput, rectsecond); //The second row croppedportion is given to a new variable
            
            OpenCvSharp.Rect[] contour_rectAL = new Rect[500];
            OpenCvSharp.Rect[] contour_rectAL1 = new Rect[500];

            toolStripProgressBar1.Value = 0;
            process++;
            imgIn.CopyTo(Sourceimg); //The image is copied to a new variable
            string time = DateTime.Now.ToString("HH:mm:ss tt");
            Cv2.CvtColor(imgInput, imgInput, ColorConversionCodes.GRAY2BGR); //Colour conversion is done to display the drawn contours
            Cv2.Rectangle(imgInput, rectfirst, Scalar.Red, 3); //Draws the rectangle based on the dimensions of rectfirst
            Cv2.Rectangle(imgInput, rectsecond, Scalar.Red, 3); //Draws the rectangle based on the dimensions of rectsecond

            Mat anode = new Mat(Sourceimg, rectfirst);
            Mat anode1 = new Mat(Sourceimg, rectsecond);
            Anode_terminal = 0;
            Anode_terminal1 = 0;
            contours = 0;
            contours1 = 0;
            Cv2.InRange(tempa, (int)numericUpDown1.Value, (int)numericUpDown2.Value, tempa); //Pixels having the value between the two bounds is considered (row 1)
            Cv2.InRange(tempaa, (int)numericUpDown3.Value, (int)numericUpDown4.Value, tempaa); //Pixels having the value between the two bounds is considered (row 2)         
            //Cv2.NamedWindow("a", WindowMode.Normal);
            //Cv2.ImShow("a", tempa);
            //Cv2.NamedWindow("aa", WindowMode.Normal);
            //Cv2.ImShow("aa", tempaa);
            OpenCvSharp.Point[][] store; //For contours
            OpenCvSharp.Point[][] store1; //For contours
            HierarchyIndex[] hey; //For contours
            HierarchyIndex[] hey1; //For contours

            Mat tempaclr = new Mat();
            tempa.CopyTo(tempaclr);
            Cv2.CvtColor(tempaclr, tempaclr, ColorConversionCodes.GRAY2BGR);
            Mat draw = new Mat(tempa.Rows, tempa.Cols, MatType.CV_8UC1, 0); //A new matrix drawn from the rows and columns of image in tempa

            Mat tempaaclr = new Mat();
            tempaa.CopyTo(tempaaclr);
            Cv2.CvtColor(tempaaclr, tempaaclr, ColorConversionCodes.GRAY2BGR);
            Mat draw1 = new Mat(tempaa.Rows, tempaa.Cols, MatType.CV_8UC1, 0); //A new matrix drawn from the rows and columns of image in tempaa

            //Contour for first
            Cv2.FindContours(tempa, out store, out hey, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple); //Finds the contour points
            OpenCvSharp.Rect rect1;
            for (int z = 0; z < store.Length; z++) //Checks for all contour points
            {
                double areafn = Cv2.ContourArea(store[z]);
                if (Cv2.ContourArea(store[z]) > 100 && Cv2.ContourArea(store[z]) < 700) //Only the contours wich has the area between 100 and 700 is allowed
                {
                    {
                        Cv2.DrawContours(draw, store, z, Scalar.White, -1, LineTypes.Link8); //Contours are drawn
                        //Cv2.NamedWindow("dd", WindowMode.Normal);
                        //Cv2.ImShow("dd", draw);
                    }
                    Scalar pen2 = new Scalar(0, 255, 0);
                    rect1 = Cv2.BoundingRect(store[z]); //Calculates bounding rectangle
                    double widthfn = rect1.Width;
                    double heightfn = rect1.Height;
                    if (rect1.Width <= 50 && rect1.Width >= 13 && rect1.Height <= 25 && rect1.Height >= 8) //Values that lie between the mentioned range is allowed to pass through
                    {
                        float aspect_ratio = (float)rect1.Width / rect1.Height;
                        int X = rect1.X + 405; //Identifies the X location of the contour
                        int Y = rect1.Y + 896; //Identifies the Y location of the contour
                        rect2 = new Rect(X, Y, rect1.Width, rect1.Height);
                        int value1 = rect2.X;
                        int value2 = rect2.Y;
                        Anode_terminal++;
                        contours++;
                        double io = Cv2.ContourArea(store[z], false);
                        contour_rectAL[contours] = new Rect(X, Y, rect1.Width, rect1.Height);
                        Cv2.Rectangle(imgInput, rect2, Scalar.Green, 3); //The rectangle is drawn along the right points found
                        double alongx = rect2.X + (rect2.Width / 2);
                        double alongy = rect2.Y + (rect2.Height / 2);

                        array2[z] = alongy; //Points sent to array for distance measurement
                        array1[z] = alongx; //Points sent to array for distance measurement
                    }
                }
            }

            //Contour for second
            Cv2.FindContours(tempaa, out store1, out hey1, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple); //Finds the contour points in the image
            OpenCvSharp.Rect rect11;
            for (int z = 0; z < store1.Length; z++) //Checks for all contour points
            {
                double areafn = Cv2.ContourArea(store1[z]);
                if (Cv2.ContourArea(store1[z]) > 100 && Cv2.ContourArea(store1[z]) < 700) //Only the contours wich has the area between 100 and 700 is allowed
                {
                    {
                        Cv2.DrawContours(draw1, store1, z, Scalar.White, -1, LineTypes.Link8); //Contours are drawn
                        //Cv2.NamedWindow("dd1", WindowMode.Normal);
                        //Cv2.ImShow("dd1", draw1);
                    }

                    Scalar pen3 = new Scalar(0, 255, 0);
                    rect11 = Cv2.BoundingRect(store1[z]); //Calculates bounding rectangle
                    double widthfn = rect11.Width;
                    double heightfn = rect11.Height;
                    if (rect11.Width <= 50 && rect11.Width >= 24 && rect11.Height <= 24 && rect11.Height >= 8) //Values that lie between the mentioned range is allowed to pass through
                    {
                        float aspect_ratio = (float)rect11.Width / rect11.Height;
                        int X = rect11.X + 405; //Identifies the X location of the contour
                        int Y = rect11.Y + 1096; //Identifies the Y location of the contour
                        rect22 = new Rect(X, Y, rect11.Width, rect11.Height);
                        Anode_terminal1++;
                        contours1++;
                        double io = Cv2.ContourArea(store1[z], false);
                        contour_rectAL1[contours1] = new Rect(X, Y, rect11.Width, rect11.Height);
                        Cv2.Rectangle(imgInput, rect22, Scalar.Lime, 3); //The rectangle is drawn along the right points found
                        double alongx = rect22.X + (rect22.Width / 2);
                        double alongy = rect22.Y + (rect22.Height / 2);

                        array22[z] = alongy; //Points sent to array for distance measurement
                        array11[z] = alongx; //Points sent to array for distance measurement
                    }
                }
            }
            
            array1 = array1.OrderByDescending(c => c).ToArray(); //The X array of the first row is sorted into descending order
            array2 = array2.OrderByDescending(c => c).ToArray(); //The Y array of the first row is sorted into descending order
            array11 = array11.OrderByDescending(c => c).ToArray(); //The X array of the second row is sorted into descending order
            array22 = array22.OrderByDescending(c => c).ToArray(); //The Y array of the secong row is sorted into descending order
            for (int z = 0; z < 12; z++)
            {
                richTextBox2.AppendText(Convert.ToString(array2[z] - array2[z + 1]) + "\n");
                richTextBox1.AppendText(Convert.ToString(array1[z]-array1[z+1]) + "\n");
                richTextBox3.AppendText(Convert.ToString(array22[z] - array22[z + 1]) + "\n");
                richTextBox4.AppendText(Convert.ToString(array11[z]-array11[z+1]) + "\n");
                //Checks the condition (Sees if the pixel range is within limit or not)
                if (array1[z] - array1[z + 1] < 108 || array1[z] - array1[z + 1] > 125 || array2[z] - array2[z + 1] > 5 || array11[z] - array11[z + 1] < 108 || array11[z] - array11[z + 1] > 125 || array22[z] - array22[z + 1] > 5) 
                {
                    midstore = false; //If failed
                    break; //Exits the loop
                }
            }
            finalpassfail = finalpassfail && midstore;
            if (midstore==true) //If the above condition is satisfied
            {
                final_pass_fail.Text = "PASS";
                final_pass_fail.BackColor = Color.LimeGreen;
                //iomodule.WriteSingleCoil(1282, true); //Sends a signal to Anode PLC through ours (Gives a high output)
                //Thread.Sleep(500); //Waits for half a second
                //iomodule.WriteSingleCoil(1282, false); //Sends a signal to Anode PLC through ours (Gives a low output)
                count = count + 1; //Increment total count
                tb_count.Text = count.ToString();
                passcount = passcount + 1; //Increment the pass count
                tb_passcount.Text = passcount.ToString();
                int countnumber = count;
                switch (count) //This is for displaying on the final board image indicating whether it has passed or not
                {
                    case 1:
                        countnumber = count;
                        lbl_pos1.Text = "PASS";
                        lbl_pos1.BackColor = Color.LimeGreen;
                        break;
                    case 2:
                        countnumber = count;
                        lbl_pos2.Text = "PASS";
                        lbl_pos2.BackColor = Color.LimeGreen;
                        break;
                    case 3:
                        countnumber = count;
                        lbl_pos3.Text = "PASS";
                        lbl_pos3.BackColor = Color.LimeGreen;
                        break;
                    case 4:
                        countnumber = count;
                        lbl_pos4.Text = "PASS";
                        lbl_pos4.BackColor = Color.LimeGreen;
                        break;
                    case 5:
                        countnumber = count;
                        lbl_pos5.Text = "PASS";
                        lbl_pos5.BackColor = Color.LimeGreen;
                        break;
                    case 6:
                        countnumber = count;
                        lbl_pos6.Text = "PASS";
                        lbl_pos6.BackColor = Color.LimeGreen;
                        break;
                    case 7:
                        countnumber = count;
                        lbl_pos7.Text = "PASS";
                        lbl_pos7.BackColor = Color.LimeGreen;
                        break;
                    case 8:
                        countnumber = count;
                        lbl_pos8.Text = "PASS";
                        lbl_pos8.BackColor = Color.LimeGreen;
                        break;

                }
            }
            else
            {
                final_pass_fail.Text = "FAIL";
                final_pass_fail.BackColor = Color.Red;
                //iomodule.WriteSingleCoil(1282, true);
                //Thread.Sleep(500);
                //iomodule.WriteSingleCoil(1282, false);
                count = count + 1; //Increments total count
                tb_count.Text = count.ToString();
                failcount = failcount + 1; //Increments fail count
                tb_failcount.Text = failcount.ToString();
                int countnumber = count;
                switch (count) //This is for displaying on the final board image indicating whether it has passed or not
                {
                    case 1:
                        countnumber = count;
                        lbl_pos1.Text = "FAIL";
                        lbl_pos1.BackColor = Color.Red;
                        break;
                    case 2:
                        countnumber = count;
                        lbl_pos2.Text = "FAIL";
                        lbl_pos2.BackColor = Color.Red;
                        break;
                    case 3:
                        countnumber = count;
                        lbl_pos3.Text = "FAIL";
                        lbl_pos3.BackColor = Color.Red;
                        break;
                    case 4:
                        countnumber = count;
                        lbl_pos4.Text = "FAIL";
                        lbl_pos4.BackColor = Color.Red;
                        break;
                    case 5:
                        countnumber = count;
                        lbl_pos5.Text = "FAIL";
                        lbl_pos5.BackColor = Color.Red;
                        break;
                    case 6:
                        countnumber = count;
                        lbl_pos6.Text = "FAIL";
                        lbl_pos6.BackColor = Color.Red;
                        break;
                    case 7:
                        countnumber = count;
                        lbl_pos7.Text = "FAIL";
                        lbl_pos7.BackColor = Color.Red;
                        break;
                    case 8:
                        countnumber = count;
                        lbl_pos8.Text = "FAIL";
                        lbl_pos8.BackColor = Color.Red;
                        break;
                }
            }
            if (count == 8) //If all the sets of the pins are inspected
            {
                //client.WriteLine(finalresult + "\t" + tb_hold.Text); //Sends the final result to the MES server along with the board's serial number
                totalcount = totalcount + 1;
                tb_totalcount.Text = totalcount.ToString();   
                if(passcount!=8) //If either one pin fails, control will be in this loop
                {
                    totalfail = totalfail + 1;
                    tb_totalfail.Text = totalfail.ToString();
                    result_pass_fail.Text = "FAIL";
                    result_pass_fail.BackColor = Color.Red;
                }
                if (passcount == 8) //Only if all pins are passed, the control will come here
                {
                    totalpass = totalpass + 1;
                    tb_totalpass.Text = totalpass.ToString();
                    result_pass_fail.Text = "PASS";
                    result_pass_fail.BackColor = Color.Green;
                }
            }
            if (count > 8)
            {
                if(midstore)
                {
                    lbl_pos1.Text = "PASS";
                    lbl_pos1.BackColor = Color.LimeGreen;
                }
                else
                {
                    lbl_pos1.Text = "FAIL";
                    lbl_pos1.BackColor = Color.Red;
                }
                tb_passcount.Text = "0"; //Resets passcount after 8 indivisual inspection
                passcount = 0;
                tb_failcount.Text = "0"; //Resets failcount after 8 indivisual inspection
                failcount = 0;
                count = 1; //Next count is set to 1 as it's the first inspection of a new board
                tb_count.Text = count.ToString();
                result_pass_fail.Text = "Result";
                result_pass_fail.BackColor = Color.LightSkyBlue;
                if (midstore) //If the first inspection of the new board is passed
                {
                    passcount = 1;
                    tb_passcount.Text = passcount.ToString();
                }
                else //If the first inspection of the new board is failed
                {
                    failcount = 1;
                    tb_failcount.Text = failcount.ToString(); ;                    
                }
                lbl_pos2.Text = "POS2";
                lbl_pos2.BackColor = Color.Cyan;
                lbl_pos3.Text = "POS3";
                lbl_pos3.BackColor = Color.Cyan;
                lbl_pos4.Text = "POS4";
                lbl_pos4.BackColor = Color.Cyan;
                lbl_pos5.Text = "POS5";
                lbl_pos5.BackColor = Color.Cyan;
                lbl_pos6.Text = "POS6";
                lbl_pos6.BackColor = Color.Cyan;
                lbl_pos7.Text = "POS7";
                lbl_pos7.BackColor = Color.Cyan;
                lbl_pos8.Text = "POS";
                lbl_pos8.BackColor = Color.Cyan;
            }

            Bitmap bmp2 = BitmapConverter.ToBitmap(imgInput);
            img_disp.Image = bmp2;
            img_disp.SizeMode = PictureBoxSizeMode.StretchImage;

            if (img_save && final_pass_fail.Text == "PASS") //if img_save is true and the output of the indivisual inspection is "PASS"
            {
                Cv2.ImWrite(@"F:\Vision_Results\Passed_Images\" + foldername + i.ToString() + ".bmp", img);
            }
            else if (img_save) //if img_save is true and the output of the indivisual inspection is "FAIL"
            {
                Cv2.ImWrite(@"F:\Vision_Results\Failed_Images\" + foldername + i.ToString() + ".bmp", img);
            }
           
        }


        private void img_disp_MouseDown(object sender, MouseEventArgs e)
        {
            //For drawing a rectangle manually (clicking down the mouse button)
            if (draw)
            {
                IsMouseDown = true;
                LocationXY = new OpenCvSharp.Point(e.X, e.Y);
            }
        }

        private void img_disp_MouseMove(object sender, MouseEventArgs e)
        {
            //For drawing a rectangle manually (moving the mouse pointer)
            if (draw)
            {
                if (IsMouseDown == true)
                {
                    LocationX1Y1 = new OpenCvSharp.Point(e.X, e.Y);
                    img_disp.Invalidate();
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            draw = true;
        }

        private void btn_test1_Click(object sender, EventArgs e)
        {
            process_image(new Mat(openFileDialog1.FileName, ImreadModes.Color), true); //Process image without calibration
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            //Accesses the TCP/IP protocal for communication
            btn_start.Enabled = false;
            client.Connect(txtHost.Text, Convert.ToInt32(txtPort.Text)); //Takes into account IP address and Port number
        }

        private void btn_stop_Click(object sender, EventArgs e)
        {
            client.Disconnect(); //Disconnects the TCP/IP protocol      
        }

        private void btn_send_Click(object sender, EventArgs e)
        {
            client.WriteLineAndGetReply(tb_hold.Text, TimeSpan.FromSeconds(3)); //Sends the message that is inside the text box
            btn_send.Enabled = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //Drawing two rectangles
            rectfirst = new Rect(491, 801, 1559, 185);
            Cv2.Rectangle(imgInput, rectfirst, Scalar.Red, 3);
            rectsecond = new Rect(491, 1001, 1569, 195);
            Cv2.Rectangle(imgInput, rectsecond, Scalar.Red, 3);

            Bitmap bmp22 = BitmapConverter.ToBitmap(imgInput);
            img_disp.Image = bmp22;
            img_disp.SizeMode = PictureBoxSizeMode.StretchImage;

            tempa = new Mat(imgInput, rectfirst);           
            tempaa = new Mat(imgInput, rectsecond);
        }

        private void pb_second_Click(object sender, EventArgs e)
        {

        }

        private void x0_capture()
        {
            //To capture image
            draw = false;
            //usbcam.Parameters[PLCamera.TriggerSource].TrySetValue(PLCamera.TriggerSource.Software);
            //usbcam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.On);            
            try
            {
                // Starts the grabbing of one image.
                usbcam.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame); //Sets the acquisition mode to single frame
                btnrun = true;
                Thread.Sleep(50);
                usbcam.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber); //Image gets grabbed one by one
                if (sftTrig) 
                    usbcam.ExecuteSoftwareTrigger(); //Software trigger is executed and image is grabbed
            }
            catch (Exception exception)
            {
                throw (exception);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            x0_capture(); //Manual capturing
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }           
        
        private void timer1_Tick(object sender, EventArgs e) //Occurs when timer is enabled
        {
            bool[] b = iomodule.ReadDiscreteInputs(1024, 1); //Reads the high input that is given to the PLC by Anode
            {
                foreach (bool bl in b)
                {
                    if (bl == true)
                    {
                        richTextBox1.Clear();
                        richTextBox2.Clear();
                        richTextBox3.Clear();
                        richTextBox4.Clear();
                        x0_capture(); //Goes to the said function
                    }
                }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //For logging images
            folderBrowserDialog2.Description = "Select Folder to Log Images";
            folderBrowserDialog2.ShowDialog();
            if (folderBrowserDialog2.SelectedPath == "")
            {
                MessageBox.Show("Images won't be logged", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            img_save = true;
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            //Waits for the QR code to get scanned
            if (qrcode.Text != "")
            {
                tb_hold.Text = qrcode.Text;
            }
        }
        
        private void txtStatus_TextChanged_1(object sender, EventArgs e)
        {
            if (txtStatus.Text == "OK\r\n")
            {
                //iomodule.WriteSingleCoil(1280, true); //If MES sends OK signal from further processing
                //iomodule.WriteSingleCoil(1281, false); //If MES sends OK signal from further processing
                //iomodule.WriteSingleCoil(1282, false);
            }
            else if (txtStatus.Text == "NOK\r\n")
            {
                //iomodule.WriteSingleCoil(1280, false); //If MES sends NOK signal from further processing
                //iomodule.WriteSingleCoil(1281, true); //If MES sends NOK signal from further processing
                //iomodule.WriteSingleCoil(1282, false);
            }
        }

        private void tb_hold_TextChanged(object sender, EventArgs e)
        {
            tb_hold.Text = qrcode.Text; //Holds the QR Code
        }

       
        private void button7_Click(object sender, EventArgs e)
        {
            //If START button is clicked
            usbcam.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
            btnrun = true;
            Thread.Sleep(50);
            //usbcam.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            usbcam.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
            button7.Enabled = false;
        }

        private void btnstop_Click(object sender, EventArgs e)
        {
            //If STOP Button is grabbed
            usbcam.StreamGrabber.Stop(); //Stops taking images
            usbcam.Dispose(); //Camera resources are disposed
            button7.Enabled = true;
        }

        private void btn_reset_Click(object sender, EventArgs e)
        {
            //Resets all the counts and labels
            count = 0;
            tb_count.Clear();
            passcount = 0;
            tb_passcount.Clear();
            failcount = 0;
            tb_failcount.Clear();
            result_pass_fail.Text = "Result";
            result_pass_fail.BackColor = Color.LightSkyBlue;
        }

        private void folderBrowserDialog2_HelpRequest(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            //Resets the labels on the board
            richTextBox1.Clear();
            richTextBox2.Clear();
            richTextBox3.Clear();
            richTextBox4.Clear();
            lbl_pos1.Text = "POS1";
            lbl_pos1.BackColor = Color.Cyan;
            lbl_pos2.Text = "POS2";
            lbl_pos2.BackColor = Color.Cyan;
            lbl_pos3.Text = "POS3";
            lbl_pos3.BackColor = Color.Cyan;
            lbl_pos4.Text = "POS4";
            lbl_pos4.BackColor = Color.Cyan;
            lbl_pos5.Text = "POS5";
            lbl_pos5.BackColor = Color.Cyan;
            lbl_pos6.Text = "POS6";
            lbl_pos6.BackColor = Color.Cyan;
            lbl_pos7.Text = "POS7";
            lbl_pos7.BackColor = Color.Cyan;
            lbl_pos8.Text = "POS";
            lbl_pos8.BackColor = Color.Cyan;
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {

        }

        private void img_disp_MouseUp(object sender, MouseEventArgs e)
        {
            //Used to draw a rectangle (Occurs when the mouse button is released from its down position)
            if (draw)
            {

                if (IsMouseDown == true)
                {
                    LocationX1Y1 = new OpenCvSharp.Point(e.X, e.Y);
                    IsMouseDown = false;
                    if (rect != null)
                    {
                        double scaleX = (double)imgInput.Width / img_disp.Size.Width;
                        double scaleY = (double)imgInput.Height / img_disp.Size.Height;
                        tx = (int)(rect.X * scaleX) + 5;
                        ty = (int)(rect.Y * scaleY) + 10;
                        tw = (int)(rect.Width * scaleX);
                        th = (int)(rect.Height * scaleY);
                        t = new Rect(tx, ty, tw, th);
                        Mat temp = new Mat(imgInput, t);
                        //Bitmap bmp = BitmapConverter.ToBitmap(temp2);
                        Bitmap bmp1 = BitmapConverter.ToBitmap(imgInput);
                        //img_disp.Image = bmp;
                        img_disp.SizeMode = PictureBoxSizeMode.StretchImage;
                        img_disp.Image = bmp1;
                        img_disp.SizeMode = PictureBoxSizeMode.StretchImage;
                    }

                }

            }
        }

        private void img_disp_Paint(object sender, PaintEventArgs e)
        {
            //To display the drawn rectangle on the image in the picture box
            if (rect != null)
            {
                e.Graphics.DrawRectangle(Pens.Red, GetRect());
            }
        }

        private Rectangle GetRect()
        {
            //For drawing the rectangle between two points in an image
            rect = new Rectangle();
            rect.X = Math.Min(LocationXY.X, LocationX1Y1.X);
            rect.Y = Math.Min(LocationXY.Y, LocationX1Y1.Y);
            rect.Width = Math.Abs(LocationXY.X - LocationX1Y1.X);
            rect.Height = Math.Abs(LocationXY.Y - LocationX1Y1.Y);
            return rect;
        }
    }
}          
