// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Attributes;
using Polytoria.Shared;
using Polytoria.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Polytoria.Datamodel.Services;

[Static("Filter"), ExplorerExclude]
[SaveIgnore]
public sealed partial class FilterService : Instance
{
	private static List<Regex> _profanityRegexes = [];
	private static HashSet<string> _normalizedProfanity = [];
	private static readonly Dictionary<int, Queue<RecentMessage>> _recentMessages = [];
	private static readonly object _contextLock = new();
	private const int ContextMessageLimit = 4;
	private static readonly TimeSpan ContextWindow = TimeSpan.FromSeconds(12);

	private readonly record struct RecentMessage(string Text, DateTime Time);

	public override void Init()
	{
		base.Init();
		LoadFilter();
	}

	private static void CompileRegexes(IEnumerable<string> rawWords)
	{
		_profanityRegexes.Clear();
		_normalizedProfanity.Clear();
		foreach (string rawWord in rawWords)
		{
			string f = rawWord.Trim();
			if (string.IsNullOrEmpty(f)) continue;

			string pattern;
			if (f.Contains('*'))
			{
				string regexSafe = Regex.Escape(f);
				pattern = $@"\b{regexSafe.Replace(@"\*", @"\w*")}\b";
			}
			else
			{
				string[] chars = new string[f.Length];
				for (int i = 0; i < f.Length; i++) chars[i] = CharPattern(f[i]);
				pattern = @"\b" + string.Join(@"[\W_]*", chars) + @"\b";
			}

			try
			{
				_profanityRegexes.Add(new Regex(pattern, RegexOptions.IgnoreCase));
				string normalized = NormalizeForMatch(f.Replace("*", ""));
				if (normalized.Length >= 3) _normalizedProfanity.Add(normalized);
			}
			catch { }
		}

		// Add static regexes to catch URLs, links, and domains
		try
		{
			// Catch http://, https://, and www.
			_profanityRegexes.Add(new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.IgnoreCase));
			// Catch domains like example.com, test.de
			_profanityRegexes.Add(new Regex(@"\b[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,}\b", RegexOptions.IgnoreCase));
		}
		catch { }
	}

	private static string CharPattern(char c)
	{
		return char.ToLowerInvariant(c) switch
		{
			'a' => @"(?:a|@|4)",
			'b' => @"(?:b|8)",
			'c' => @"(?:c|\(|¢)",
			'e' => @"(?:e|3)",
			'g' => @"(?:g|9)",
			'i' => @"(?:i|1|!|\||l)",
			'l' => @"(?:l|1|!|\||i)",
			'o' => @"(?:o|0)",
			's' => @"(?:s|\$|5|z)",
			't' => @"(?:t|7|\+)",
			'u' => @"(?:u|v|ü)",
			'v' => @"(?:v|u)",
			'z' => @"(?:z|2|s)",
			_ => Regex.Escape(c.ToString())
		};
	}

	private static async void LoadFilter()
	{
		try
		{
			if (OS.HasFeature("offline"))
			{
				CompileRegexes(["swear"]);
				return;
			}
			string rawdata = await PolyAPI.GetProfanityList();
			CompileRegexes(rawdata.Split(["\n", "\r"], StringSplitOptions.RemoveEmptyEntries));
		}
		catch
		{
			PT.PrintErr("Failed to get profanity list from API. Using local fallback.");
			try
			{
				if (Godot.FileAccess.FileExists("res://profanity.txt"))
				{
					using Godot.FileAccess file = Godot.FileAccess.Open("res://profanity.txt", Godot.FileAccess.ModeFlags.Read);
					CompileRegexes(file.GetAsText().Split(["\n", "\r"], StringSplitOptions.RemoveEmptyEntries));
				}
				else
				{
					CompileRegexes(["badword"]);
				}
			}
			catch
			{
				CompileRegexes(["badword"]);
			}
		}
	}

	[ScriptMethod]
	public static string Filter(string input)
	{
		if (_profanityRegexes.Count == 0 || string.IsNullOrWhiteSpace(input))
		{
			if (_profanityRegexes.Count == 0) LoadFilter();
			return input;
		}

		string filtered = input;
		foreach (Regex regex in _profanityRegexes)
		{
			try
			{
				filtered = regex.Replace(filtered, m => new string('*', m.Length));
			}
			catch { }
		}

		return filtered;
	}

	public static string FilterForPlayer(int userId, string input)
	{
		string filtered = Filter(input);
		bool blocked = ContainsNormalizedProfanity(input, HasBypassSignals(input)) || CompletesRecentProfanity(userId, input);
		if (blocked)
		{
			ClearContext(userId);
			return MaskVisible(input);
		}

		TrackMessage(userId, input);
		return filtered;
	}

	public static void ClearContext(int userId)
	{
		lock (_contextLock)
		{
			_recentMessages.Remove(userId);
		}
	}

	private static bool CompletesRecentProfanity(int userId, string input)
	{
		if (_normalizedProfanity.Count == 0) return false;

		string current = NormalizeForMatch(input);
		if (current.Length == 0) return false;

		lock (_contextLock)
		{
			if (!_recentMessages.TryGetValue(userId, out Queue<RecentMessage>? messages)) return false;

			DateTime now = DateTime.UtcNow;
			while (messages.Count > 0 && now - messages.Peek().Time > ContextWindow) messages.Dequeue();

			if (messages.Count == 0) return false;

			StringBuilder combined = new();
			foreach (RecentMessage message in messages) combined.Append(NormalizeForMatch(message.Text));
			combined.Append(current);
			string combinedText = combined.ToString();

			foreach (string word in _normalizedProfanity)
			{
				if (word.Length >= 4 && combinedText.Contains(word, StringComparison.Ordinal)) return true;
				if (word.Length == 3 && combinedText == word) return true;
			}
		}

		return false;
	}

	private static void TrackMessage(int userId, string input)
	{
		string normalized = NormalizeForMatch(input);
		if (normalized.Length == 0) return;

		lock (_contextLock)
		{
			if (!_recentMessages.TryGetValue(userId, out Queue<RecentMessage>? messages))
			{
				messages = new();
				_recentMessages[userId] = messages;
			}

			DateTime now = DateTime.UtcNow;
			messages.Enqueue(new(input, now));
			while (messages.Count > ContextMessageLimit || now - messages.Peek().Time > ContextWindow) messages.Dequeue();
		}
	}

	private static bool ContainsNormalizedProfanity(string input, bool aggressive)
	{
		if (_normalizedProfanity.Count == 0) return false;

		string normalized = NormalizeForMatch(input);
		if (normalized.Length == 0) return false;

		foreach (string word in _normalizedProfanity)
		{
			if (word.Length >= 4 && aggressive && normalized.Contains(word, StringComparison.Ordinal)) return true;
			if (word.Length == 3 && normalized == word) return true;
		}

		return false;
	}

	private static bool HasBypassSignals(string input)
	{
		int letters = 0;
		int separators = 0;
		int substitutions = 0;
		char last = '\0';
		int repeat = 1;

		foreach (char c in input)
		{
			if (char.IsLetter(c))
			{
				letters++;
				char lower = char.ToLowerInvariant(c);
				if (lower == last) repeat++;
				else
				{
					last = lower;
					repeat = 1;
				}
				if (repeat >= 3) return true;
			}
			else if (char.IsDigit(c) || "@$!|()+".Contains(c))
			{
				substitutions++;
			}
			else if (!char.IsWhiteSpace(c))
			{
				separators++;
			}
		}

		return letters >= 2 && (substitutions > 0 || separators > 0);
	}

	private static string NormalizeForMatch(string input)
	{
		string decomposed = input.Normalize(NormalizationForm.FormD);
		StringBuilder sb = new();
		char last = '\0';

		foreach (char raw in decomposed)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark) continue;

			char c = char.ToLowerInvariant(raw);
			c = c switch
			{
				'@' or '4' => 'a',
				'8' => 'b',
				'(' or '¢' => 'c',
				'3' => 'e',
				'9' => 'g',
				'1' or '!' or '|' => 'i',
				'0' => 'o',
				'$' or '5' => 's',
				'7' or '+' => 't',
				'ü' => 'u',
				'2' => 'z',
				_ => c
			};

			if (!char.IsLetterOrDigit(c)) continue;
			if (c == last) continue;

			sb.Append(c);
			last = c;
		}

		return sb.ToString();
	}

	private static string MaskVisible(string input)
	{
		StringBuilder sb = new(input.Length);
		foreach (char c in input) sb.Append(char.IsWhiteSpace(c) ? c : '*');
		return sb.ToString();
	}
}
