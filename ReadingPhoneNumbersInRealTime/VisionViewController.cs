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
			if (Request != null)
				Request = new VNRecognizeTextRequest (RecognizeTextHandler);
			base.ViewDidLoad ();
		}

		public void RecognizeTextHandler (VNRequest request, NSError error)
		{
			List<string> numbers = new List<string> ();
			List<CGRect> redBoxes = new List<CGRect> ();
			List<CGRect> greenBoxes = new List<CGRect> ();

			var results = request.GetResults<VNRecognizedTextObservation> ();
			var maximumCandidates = 1;

			foreach (var visionResult in results) {
				var candidate = visionResult.TopCandidates ((nuint)maximumCandidates) [0];
				if (candidate != null) {
					var numberIsSubstring = true;
					var result = candidate.String.ExtractPhoneNumber ();
					var range = result.range;
					var number = result.result;
					if (number != null) {
						var boundingBox = candidate.GetBoundingBox (range, out NSError boundingBoxError);
						if (boundingBoxError == null) {
							var box = boundingBox.BoundingBox;
							numbers.Add (number);
							greenBoxes.Add (box);
							//numberIsString = !(range.Location == candidate.GetLowerBound (0) && range.Length == candidate.GetUpperBound (0) - candidate.GetLowerBound (0));
						} else {
							boundingBoxError.Dispose ();
						}
					}
					if (numberIsSubstring)
						redBoxes.Add (visionResult.BoundingBox);
				}
			}

			NumberTracker.LogFrame (numbers.ToArray ());

			(CGColor color, CGRect [] boxes) [] boxGroups = { (UIColor.Red.CGColor, redBoxes.ToArray ()), (UIColor.Green.CGColor, greenBoxes.ToArray ()) };

			Show (boxGroups);

			var sureNumber = NumberTracker.GetStableString ();
			if (sureNumber != null) {
				ShowString (sureNumber);
				NumberTracker.Reset (sureNumber);
			}
		}

		[Export ("captureOutput:didOutputSampleBuffer:fromConnection:")]
		public void DidOutputSampleBuffer (AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
		{
			// Free memory here, otherwise buffer will end up dropping frames
			using (sampleBuffer)
			using (var pixelBuffer = sampleBuffer.GetImageBuffer ()) {
				if (pixelBuffer != null) {
					Request.RecognitionLevel = VNRequestTextRecognitionLevel.Fast;
					Request.UsesLanguageCorrection = false;
					Request.RegionOfInterest = RegionOfInterest;
					using (var requestHandler = new VNImageRequestHandler ((CoreVideo.CVPixelBuffer)pixelBuffer, TextOrientation, new NSDictionary ())) {
						VNRequest [] requests = { Request };
						requestHandler.Perform (requests, out NSError requestError);
						if (requestError != null) {
							Console.WriteLine ("requestError - " + requestError);
							requestError.Dispose ();
						}
					}
				}
			}
		}

		[Export ("captureOutput:didDropSampleBuffer:fromConnection:")]
		public void DidDropSampleBuffer (AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
		{
			Console.WriteLine ("Frame dropped");
		}

		public void Draw (CGRect rect, CGColor color)
		{
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
			BoxLayer.RemoveAll ((layer) => {
				layer.RemoveFromSuperLayer ();
				return true;
			});
		}

		public void Show ((CGColor color, CGRect [] boxes) [] boxGroups)
		{
			DispatchQueue.MainQueue.DispatchAsync (() => {
				RemoveBoxes ();
				foreach (var (color, boxes) in boxGroups) {
					foreach (var box in boxes) {
						var temp = CGAffineTransform.CGRectApplyAffineTransform (box, VisionToAVFTransform);
						var rect = Preview.VideoPreviewLayer.MapToLayerCoordinates (temp);
						Draw (rect, color);
					}
				}
			});
		}
	}
}



