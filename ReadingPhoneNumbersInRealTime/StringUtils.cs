using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Foundation;

namespace ReadingPhoneNumbersInRealTime {

	public static class CharacterExtension {
		public static char GetSimilarCharacterIfNotIn (this char thisChar, string allowedChars)
		{
			Dictionary<string, string> conversionTable = new Dictionary<string, string> {
				{ "s", "S" },
				{ "S", "5" },
				{ "5", "S" },
				{ "o", "O" },
				{ "Q", "O" },
				{ "O", "0" },
				{ "0", "O" },
				{ "l", "I" },
				{ "I", "1" },
				{ "1", "I" },
				{ "B", "8" },
				{ "8", "B" }
			};
			var maxSubstitutions = 2;
			string current = thisChar.ToString ();
			var counter = 0;
			while (allowedChars.Contains(current) && counter < maxSubstitutions) {
				var altChar = conversionTable [current];
				if (altChar != null) {
					current = altChar;
					counter += 1;
				} else {
					break;
				}
			}
			return current [0]; // convert to Char.
		}
	}

	public static class StringExtension {
		public static (NSRange range, string result) ExtractPhoneNumber (this string thisString) //Is this doing what it is supposed to be doing?
		{
			string pattern = @"(?x)					# Verbose regex, allows comments
					   (?:\+1-?)?				# Potential international prefix, may have -
					   [(]?					# Potential opening (
					   \b(\w{3})				# Capture xxx
					   [)]?					# Potential closing )
					   [\ -./]?				# Potential separator
					   (\w{3})				# Capture xxx
					   [\ -./] ?				# Potential separator
					   (\w{4})\b				# Capture xxxx";
			Match match = Regex.Match (thisString, pattern);
			NSRange range = new NSRange (match.Index, match.Length); // null check here
			string phoneNumberDigits = match.Value;
			//string substring = thisString.Substring (match.Index, match.Length);
			////var nsrange = new NSRange ();
			//Match match1 = Regex.Match (substring, pattern);
			if (phoneNumberDigits.Length != 10)
				return (range, null);
				//throw new Exception ("Unexpected runtime error."); // return null for tuple?

			var result = "";
			string allowedChars = "0123456789";
			foreach (var chr in phoneNumberDigits) {
				var chr2 = chr.GetSimilarCharacterIfNotIn (allowedChars);
				if (!allowedChars.Contains(chr2))
					throw new Exception ("Unexpected runtime error."); // return null for tuple?
				result += chr2;
			}

			return (range, result);
		}
	}


	public class StringTracker {
		public long FrameIndex;
		public Dictionary<string, (long lastSeen, long count)> SeenStrings;
		public long BestCount;
		public string BestString;

		public StringTracker ()
		{
			FrameIndex = 0;
			SeenStrings = new Dictionary<string, (long lastSeen, long count)> ();
			BestCount = 0;
			BestString = "";
		}

		public void LogFrame (string[] strs)
		{
			foreach (var str in strs) {
				if (!SeenStrings.ContainsKey (str))
					SeenStrings.Add (str, (0, -1));
				var val = SeenStrings [str];
				SeenStrings [str] = (FrameIndex, val.count + 1);
				Console.WriteLine ("Seen {0} {1} times", str, SeenStrings [str].count);
			}
			List<string> obsoleteStrings = new List<string> ();
			foreach (var (str, val) in SeenStrings) {
				if (val.lastSeen < FrameIndex - 30) {
					obsoleteStrings.Add (str);
				}
				var count = val.count;
				if (!obsoleteStrings.Contains(str) && count > BestCount) {
					BestCount = count;
					BestString = str;
				}
			}
			obsoleteStrings.ForEach ((str) => { SeenStrings.Remove (str); });
			FrameIndex += 1;
		}

		public string GetStableString ()
		{
			if (BestCount >= 10)
				return BestString;
			return null;
		}

		public void Reset(string str)
		{
			if (str != null && SeenStrings.ContainsKey (str)) {
				SeenStrings.Remove (str);
				BestCount = 0;
				BestString = "";
			}
		}
	}
}
