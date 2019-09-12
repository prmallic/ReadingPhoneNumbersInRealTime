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
			while (!allowedChars.Contains (current) && counter < maxSubstitutions) {
				if (conversionTable.ContainsKey (current)) {
					current = conversionTable [current];
					counter += 1;
				} else {
					break;
				}
			}
			return current [0];
		}
	}

	public static class StringExtension {
		public static (NSRange range, string result) ExtractPhoneNumber (this string thisString)
		{
			string pattern = @"(?:\+1-?)?[(]?\b(\w{3})[)]?[\ -\.]?(\w{3})[\ -\.]?(\w{4})\b";
			Match match = Regex.Match (thisString, pattern);
			NSRange range = new NSRange (match.Index, match.Length);
			string phoneNumberDigits = match.Value;

			if (phoneNumberDigits.Length != 10)
				return (range, null);

			var result = "";
			string allowedChars = "0123456789";
			foreach (char chr in phoneNumberDigits) {
				char chr2 = chr.GetSimilarCharacterIfNotIn (allowedChars);
				if (!allowedChars.Contains (chr2))
					return (range, null);
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

		public void LogFrame (string [] strs)
		{
			foreach (var str in strs) {
				if (!SeenStrings.ContainsKey (str))
					SeenStrings.Add (str, (0, -1));
				var (lastSeen, count) = SeenStrings [str];
				SeenStrings [str] = (FrameIndex, count + 1);
				Console.WriteLine ("Seen {0} {1} times", str, SeenStrings [str].count);
			}
			List<string> obsoleteStrings = new List<string> ();
			foreach (var (str, val) in SeenStrings) {
				if (val.lastSeen < FrameIndex - 30) {
					obsoleteStrings.Add (str);
				}
				var count = val.count;
				if (!obsoleteStrings.Contains (str) && count > BestCount) {
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

		public void Reset (string str)
		{
			if (str != null && SeenStrings.ContainsKey (str)) {
				SeenStrings.Remove (str);
				BestCount = 0;
				BestString = "";
			}
		}
	}
}
