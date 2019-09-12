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

namespace ReadingPhoneNumbersInRealTime {
	public partial class VisionViewController : ViewController {
		public VNRecognizeTextRequest Request;
		public StringTracker NumberTracker;
		public List<CAShapeLayer> BoxLayer;
		public VisionViewController (IntPtr handle) : base (handle)
		{
			NumberTracker = new StringTracker ();
			BoxLayer = new List<CAShapeLayer> ();
		}

		public override void ViewDidLoad ()
		{
			Console.WriteLine ("VisionViewController ViewDidLoad");
			Request = new VNRecognizeTextRequest (RecognizeTextHandler);
			base.ViewDidLoad ();
		}

		public void RecognizeTextHandler(VNRequest request, NSError error)
		{
			Console.WriteLine ("RecognizeTextHandler");
			List<string> numbers = new List<string> ();
			List<CGRect> redBoxes = new List<CGRect> ();
			List<CGRect> greenBoxes = new List<CGRect> ();

			var results = request.GetResults<VNRecognizedTextObservation> (); //check here
			Console.WriteLine (results.Length);
			var maximumCandidates = 1;

			foreach (var visionResult in results) {
				var candidate = visionResult.TopCandidates ((nuint)maximumCandidates)[0]; //guard
				if (candidate != null) {
					var numberIsString = true;
					var result = candidate.String.ExtractPhoneNumber (); //null check
					var range = result.range;
					var number = result.result;
					if (number != null) {
						var boundingBox = candidate.GetBoundingBox (range, out NSError nS); // check error
						if (nS == null) {
							var box = boundingBox.BoundingBox; //null check
							numbers.Add (number);
							greenBoxes.Add (box);
							//numberIsString = !(range.Location == candidate.GetLowerBound (0) && range.Length == candidate.GetUpperBound (0) - candidate.GetLowerBound (0)); // change this
						}
					}
					if (numberIsString)
						redBoxes.Add (visionResult.BoundingBox);
				}
			}
			Console.WriteLine (redBoxes.Count);
			Console.WriteLine (greenBoxes.Count);

			NumberTracker.LogFrame (numbers.ToArray ());
			Show (UIColor.Red.CGColor, redBoxes.ToArray ());
			Show (UIColor.Green.CGColor, greenBoxes.ToArray ());

			var sureNumber = NumberTracker.GetStableString (); // null check
			if (sureNumber != null) {
				ShowString (sureNumber);
				NumberTracker.Reset (sureNumber);
			}
		}

		[Export ("captureOutput:didOutputSampleBuffer:fromConnection:")] //CaptureOutput
		public void DidOutputSampleBuffer (AVCaptureOutput captureOutput, CoreMedia.CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
		{
			Console.WriteLine ("DidOutputSampleBuffer CaptureOutput "+ this.GetType().Name);
			var pixelBuffer = sampleBuffer.GetImageBuffer (); // null check
			if (pixelBuffer != null) {
				Request.RecognitionLevel = VNRequestTextRecognitionLevel.Fast;
				Request.UsesLanguageCorrection = false;
				Request.RegionOfInterest = RegionOfInterest;
				var requestHandler = new VNImageRequestHandler ((CoreVideo.CVPixelBuffer)pixelBuffer, TextOrientation, new NSDictionary ());
				VNRequest [] requests = { Request };
				requestHandler.Perform (requests, out NSError requestError);
				if (requestError != null)
					Console.WriteLine (requestError);
			}
		}

		public void Draw(CGRect rect, CGColor color)
		{
			Console.WriteLine ("Draw");
			var layer = new CAShapeLayer {
				Opacity = 0.5f,
				BorderColor = color,
				BorderWidth = 1,
				Frame = rect
			};
			BoxLayer.Add (layer);
			Preview.VideoPreviewLayer.InsertSublayer (layer, 1);
		}

		public void RemoveBoxes ()
		{
			Console.WriteLine ("RemoveBoxes");
			BoxLayer.RemoveAll ((layer) => {
				layer.RemoveFromSuperLayer ();
				return true;
			});
		}

		public void Show (CGColor color, CGRect [] boxes) // array of these tuples? // public void Show (Tuple<CGColor color, CGRect [] boxes> []) // array of these tuples?
		{
			Console.WriteLine ("Show");
			DispatchQueue.MainQueue.DispatchAsync (() => { //make asynch
				var layer = Preview.VideoPreviewLayer;
				RemoveBoxes ();
				foreach (var box in boxes) {
					//VisionToAVFTransform = //concats of ROIToGlobalTransform +BottomToTopTransform + UIRotationTransform;
					var temp = CGAffineTransform.CGRectApplyAffineTransform (box, ROIToGlobalTransform);
					temp = CGAffineTransform.CGRectApplyAffineTransform (temp, BottomToTopTransform);
					temp = CGAffineTransform.CGRectApplyAffineTransform (temp, UIRotationTransform);
					var rect = layer.MapToLayerCoordinates (temp);
					Draw (rect, color);
				}
			});
		}
	}
}



