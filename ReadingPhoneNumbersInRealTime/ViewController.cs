using Foundation;
using CoreGraphics;
using System;
using System.Collections.Generic;
using UIKit;
using Vision;
using VisionKit;
using AVFoundation;
using CoreFoundation;
using CoreAnimation;
using CoreMedia;
using CoreVideo;

namespace ReadingPhoneNumbersInRealTime {
	public partial class ViewController : UIViewController, IAVCaptureVideoDataOutputSampleBufferDelegate {
		public CAShapeLayer MaskLayer;
		public UIDeviceOrientation CurrentOrientation;
		AVCaptureSession captureSession;
		public AVCaptureDevice CaptureDevice;
		public AVCaptureVideoDataOutput VideoDataOutput;
		public DispatchQueue VideoDataOutputQueue;
		public CGRect RegionOfInterest;
		public ImageIO.CGImagePropertyOrientation TextOrientation;
		public double BufferAspectRatio;
		public CGAffineTransform UIRotationTransform;
		public CGAffineTransform BottomToTopTransform;
		public CGAffineTransform ROIToGlobalTransform;
		public CGAffineTransform VisionToAVFTransform;
		UITapGestureRecognizer gestureRecognizer;

		public ViewController (IntPtr handle) : base (handle)
		{
			MaskLayer = new CAShapeLayer ();
			CurrentOrientation = UIDeviceOrientation.Portrait;
			captureSession = new AVCaptureSession ();
			VideoDataOutput = new AVCaptureVideoDataOutput ();
			VideoDataOutputQueue = new DispatchQueue ("VideoDataOutputQueue");
			RegionOfInterest = new CGRect (0, 0, 1, 1);
			TextOrientation = ImageIO.CGImagePropertyOrientation.Up;
			UIRotationTransform = CGAffineTransform.MakeIdentity ();
			BottomToTopTransform = CGAffineTransform.MakeScale (1, -1);
			BottomToTopTransform = CGAffineTransform.Translate (BottomToTopTransform, 0, -1);
			ROIToGlobalTransform = CGAffineTransform.MakeIdentity ();
			gestureRecognizer = new UITapGestureRecognizer ((gesture) => {
				HandleTap ();
			});
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			Preview.Session = captureSession;
			CutoutView.BackgroundColor = UIColor.Gray.ColorWithAlpha (0.5f);
			MaskLayer.BackgroundColor = UIColor.Clear.CGColor;
			MaskLayer.FillRule = CAShapeLayer.FillRuleEvenOdd;
			CutoutView.Layer.Mask = MaskLayer;
			SetupCamera ();
			CalculateRegionOfInterest ();
			CutoutView.AddGestureRecognizer (gestureRecognizer);
		}

		public override void ViewWillTransitionToSize (CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
		{
			base.ViewWillTransitionToSize (toSize, coordinator);
			UIDeviceOrientation deviceOrientation = UIDevice.CurrentDevice.Orientation;
			if (deviceOrientation.IsPortrait () || deviceOrientation.IsLandscape ())
				CurrentOrientation = deviceOrientation;
			AVCaptureConnection videoPreviewLayerConnection = Preview.VideoPreviewLayer.Connection;
			if (videoPreviewLayerConnection != null)
				videoPreviewLayerConnection.VideoOrientation = (AVCaptureVideoOrientation)deviceOrientation;
			CalculateRegionOfInterest ();
		}

		public override void ViewDidLayoutSubviews ()
		{
			base.ViewDidLayoutSubviews ();
			UpdateCutOut ();
		}

		public override void DidReceiveMemoryWarning ()
		{
			base.DidReceiveMemoryWarning ();
		}

		public void CalculateRegionOfInterest ()
		{
			var desiredHeightRatio = 0.15;
			var desiredWidthRatio = 0.6;
			var maxPortraitWidth = 0.8;
			CGSize size = CurrentOrientation.IsPortrait () || CurrentOrientation == UIDeviceOrientation.Unknown
				? new CGSize (Math.Min (desiredWidthRatio * BufferAspectRatio, maxPortraitWidth), desiredHeightRatio / BufferAspectRatio)
				: new CGSize (desiredWidthRatio, desiredHeightRatio);
			RegionOfInterest = new CGRect ((1 - size.Width) / 2, (1 - size.Height) / 2, size.Width, size.Height);
			SetupOrientationAndTransform ();
			DispatchQueue.MainQueue.DispatchAsync (UpdateCutOut);
		}

		public void UpdateCutOut ()
		{
			var roiRectTransform = CGAffineTransform.Multiply (BottomToTopTransform, UIRotationTransform);
			var temp = CGAffineTransform.CGRectApplyAffineTransform (RegionOfInterest, roiRectTransform);
			var cutout = Preview.VideoPreviewLayer.MapToLayerCoordinates (temp);
			var path = UIBezierPath.FromRect (CutoutView.Frame);
			path.AppendPath (UIBezierPath.FromRect (cutout));
			MaskLayer.Path = path.CGPath;
			var numFrame = cutout;
			numFrame.Y += numFrame.Height;
			NumberView.Frame = numFrame;
		}

		public void SetupOrientationAndTransform ()
		{
			var roi = RegionOfInterest;
			ROIToGlobalTransform = CGAffineTransform.MakeTranslation (roi.X, roi.Y);
			ROIToGlobalTransform = CGAffineTransform.Scale (ROIToGlobalTransform, roi.Width, roi.Height);
			switch (CurrentOrientation) {
			case UIDeviceOrientation.LandscapeLeft:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Up;
				UIRotationTransform = CGAffineTransform.MakeIdentity ();
				break;
			case UIDeviceOrientation.LandscapeRight:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Down;
				UIRotationTransform = CGAffineTransform.MakeTranslation (1, 1);
				UIRotationTransform = CGAffineTransform.Rotate (UIRotationTransform, (nfloat)Math.PI);
				break;
			case UIDeviceOrientation.PortraitUpsideDown:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Left;
				UIRotationTransform = CGAffineTransform.MakeTranslation (1, 0);
				UIRotationTransform = CGAffineTransform.Rotate (UIRotationTransform, (nfloat)Math.PI / 2);
				break;
			default:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Right;
				UIRotationTransform = CGAffineTransform.MakeTranslation (0, 1);
				UIRotationTransform = CGAffineTransform.Rotate (UIRotationTransform, -(nfloat)Math.PI / 2);
				break;
			}
			VisionToAVFTransform = CGAffineTransform.Multiply (ROIToGlobalTransform, BottomToTopTransform);
			VisionToAVFTransform = CGAffineTransform.Multiply (VisionToAVFTransform, UIRotationTransform);
		}

		public void SetupCamera ()
		{
			AVCaptureDevice captureDevice = AVCaptureDevice.GetDefaultDevice (AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaTypes.Video, AVCaptureDevicePosition.Back);
			if (captureDevice == null) {
				Console.WriteLine ("Could not create capture device");
				return;
			}
			CaptureDevice = captureDevice;
			if (captureDevice.SupportsAVCaptureSessionPreset (AVCaptureSession.Preset3840x2160)) {
				captureSession.SessionPreset = AVCaptureSession.Preset3840x2160;
				BufferAspectRatio = 3840.0 / 2160.0;
			} else {
				captureSession.SessionPreset = AVCaptureSession.Preset1920x1080;
				BufferAspectRatio = 1920.0 / 1080.0;
			}

			AVCaptureDeviceInput deviceInput = new AVCaptureDeviceInput (captureDevice, out NSError deviceInputErr);
			if (deviceInputErr != null) {
				Console.WriteLine ("Could not create device input");
				deviceInputErr.Dispose ();
				return;
			}
			if (captureSession.CanAddInput (deviceInput))
				captureSession.AddInput (deviceInput);

			VideoDataOutput.AlwaysDiscardsLateVideoFrames = true;
			VideoDataOutput.SetSampleBufferDelegateQueue (this, VideoDataOutputQueue);
			//VideoDataOutput.WeakVideoSettings = new NSDictionary<NSString, NSString> ();
			//VideoDataOutput.WeakVideoSettings.TryAdd<NSString, NSString> (CVPixelBuffer.PixelFormatTypeKey, OSType);

			if (captureSession.CanAddOutput (VideoDataOutput)) {
				captureSession.AddOutput (VideoDataOutput);
				VideoDataOutput.ConnectionFromMediaType (AVMediaType.Video).PreferredVideoStabilizationMode = AVCaptureVideoStabilizationMode.Off;
			} else {
				Console.WriteLine ("Could not add VDO output");
			}

			_ = captureDevice.LockForConfiguration (out NSError lockConf);
			if (lockConf != null) {
				Console.WriteLine ("Could not set zoom level due to error: " + lockConf);
				lockConf.Dispose ();
				return;
			}
			captureDevice.VideoZoomFactor = 2;
			captureDevice.AutoFocusRangeRestriction = AVCaptureAutoFocusRangeRestriction.Near;
			captureDevice.UnlockForConfiguration ();

			captureSession.StartRunning ();
		}

		public void ShowString (string str)
		{
			DispatchQueue.MainQueue.DispatchSync (captureSession.StopRunning);
			DispatchQueue.MainQueue.DispatchSync (() => {
				NumberView.Text = str;
				NumberView.Hidden = false;
			});
		}

		private void HandleTap ()
		{
			DispatchQueue.MainQueue.DispatchAsync (() => {
				NumberView.Hidden = true;
				if (!captureSession.Running) {
					captureSession.StartRunning ();
				}
			});
		}
	}

}