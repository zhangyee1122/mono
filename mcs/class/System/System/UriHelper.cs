using System;
using System.Globalization;
using System.Text;

namespace System {
	internal class UriHelper {
		internal const UriFormat ToStringUnescape = (UriFormat) 0x7FFF;

		internal static bool IriParsing	{
			get { return Uri.IriParsing; }
		}

		[Flags]
		internal enum UriSchemes {
			Http = 1 << 0,
			Https = 1 << 1,
			File = 1 << 2,
			Ftp = 1 << 3,
			Gopher = 1 << 4,
			Ldap = 1 << 5,
			Mailto = 1 << 6,
			NetPipe = 1 << 7,
			NetTcp = 1 << 8,
			News = 1 << 9,
			Nntp = 1 << 10,
			Telnet = 1 << 11,
			Uuid = 1 << 12,
			Custom = 1 << 13,
			All = ~0,
			None = 0
		}

		private static UriSchemes GetScheme (string schemeName)
		{
			if (schemeName == "")
				return UriSchemes.None;
			if (schemeName == Uri.UriSchemeHttp)
				return UriSchemes.Http;
			if (schemeName == Uri.UriSchemeHttps)
				return UriSchemes.Https;
			if (schemeName == Uri.UriSchemeFile)
				return UriSchemes.File;
			if (schemeName == Uri.UriSchemeFtp)
				return UriSchemes.Ftp;
			if (schemeName == Uri.UriSchemeGopher)
				return UriSchemes.Gopher;
			if (schemeName == Uri.UriSchemeLdap)
				return UriSchemes.Ldap;
			if (schemeName == Uri.UriSchemeMailto)
				return UriSchemes.Mailto;
			if (schemeName == Uri.UriSchemeNetPipe)
				return UriSchemes.NetPipe;
			if (schemeName == Uri.UriSchemeNetTcp)
				return UriSchemes.NetTcp;
			if (schemeName == Uri.UriSchemeNews)
				return UriSchemes.News;
			if (schemeName == Uri.UriSchemeNntp)
				return UriSchemes.Nntp;
			if (schemeName == Uri.UriSchemeTelnet)
				return UriSchemes.Telnet;
			if (schemeName == Uri.UriSchemeUuid)
				return UriSchemes.Uuid;

			return UriSchemes.Custom;
		}

		internal static bool SchemeContains (UriSchemes keys, UriSchemes flag)
		{
			return (keys & flag) != 0;
		}

		internal static string HexEscapeMultiByte (char character)
		{
			const string hex_upper_chars = "0123456789ABCDEF";
			string ret = "";
			byte [] bytes = Encoding.UTF8.GetBytes (new [] {character});
			foreach (byte b in bytes)
				ret += "%" + hex_upper_chars [((b & 0xf0) >> 4)] + hex_upper_chars [((b & 0x0f))];

			return ret;
		}

		internal static bool SupportsQuery (string scheme)
		{
			return SupportsQuery (GetScheme (scheme));
		}

		internal static bool SupportsQuery(UriSchemes scheme)
		{
			if (SchemeContains (scheme, UriSchemes.File))
				return IriParsing;

			return !SchemeContains (scheme, UriSchemes.Ftp | UriSchemes.Gopher | UriSchemes.Nntp | UriSchemes.Telnet);
		}

		internal static string Format (string str, string schemeName, UriKind uriKind,
			UriComponents component, UriFormat uriFormat)
		{
			if (string.IsNullOrEmpty (str))
				return "";

			UriSchemes scheme = GetScheme (schemeName);

			var s = new StringBuilder ();
			int len = str.Length;
			for (int i = 0; i < len; i++) {
				char c = str [i];
				if (c == '%') {
					char surrogate;
					char x = Uri.HexUnescapeMultiByte (str, ref i, out surrogate);

					if (surrogate != char.MinValue) {
						s.Append (FormatChar (x, true, scheme, uriKind, component, uriFormat));
						s.Append (surrogate);
					} else
						s.Append (FormatChar (x, true, scheme, uriKind, component, uriFormat));

					i--;
				} else
					s.Append (FormatChar (c, false, scheme, uriKind, component, uriFormat));
			}

			return s.ToString ();
		}

		internal static string FormatChar (char c, bool isEscaped, UriSchemes scheme, UriKind uriKind,
			UriComponents component, UriFormat uriFormat)
		{
			if (!isEscaped && NeedToEscape (c, scheme, component, uriKind, uriFormat) ||
				isEscaped && !NeedToUnescape (c, scheme, component, uriKind, uriFormat))
				return HexEscapeMultiByte (c);

			if (c == '\\' && component == UriComponents.Path) {
				if (!IriParsing && uriFormat != UriFormat.UriEscaped &&
					SchemeContains (scheme, UriSchemes.Http | UriSchemes.Https))
					return "/";

				if (SchemeContains (scheme, UriSchemes.Http | UriSchemes.Https | UriSchemes.Ftp | UriSchemes.Custom))
					return (isEscaped && uriFormat != UriFormat.UriEscaped) ? "\\" : "/";

				if (SchemeContains (scheme, UriSchemes.NetPipe | UriSchemes.NetTcp))
					return "/";

				if (SchemeContains (scheme, UriSchemes.File))
					return "/";
			}

			return c.ToString (CultureInfo.InvariantCulture);
		}

		private static bool NeedToUnescape (char c, UriSchemes scheme, UriComponents component, UriKind uriKind,
			UriFormat uriFormat)
		{
			string cStr = c.ToString (CultureInfo.InvariantCulture);

			if (uriFormat == UriFormat.Unescaped)
				return true;

			UriSchemes sDecoders = UriSchemes.NetPipe | UriSchemes.NetTcp;

			if (!IriParsing)
				sDecoders |= UriSchemes.Http | UriSchemes.Https;

			if (c == '/' || c == '\\') {
				if (!IriParsing && uriKind == UriKind.Absolute && uriFormat != UriFormat.UriEscaped &&
					uriFormat != UriFormat.SafeUnescaped)
					return true;

				if (SchemeContains (scheme, UriSchemes.File)) {
					return component != UriComponents.Fragment &&
						   (component != UriComponents.Query || !IriParsing);
				}

				return component != UriComponents.Query && component != UriComponents.Fragment &&
					   SchemeContains (scheme, sDecoders);
			}

			if (c == '?') {
				//Avoid creating new query
				if (SupportsQuery (scheme) && component == UriComponents.Path)
					return false;

				if (!IriParsing && uriFormat == ToStringUnescape) {
					if (SupportsQuery (scheme))
						return component == UriComponents.Query || component == UriComponents.Fragment;

					return component == UriComponents.Fragment;
				}

				return false;
			}

			if (c == '#') {
				//Avoid creating new fragment
				if (component == UriComponents.Path || component == UriComponents.Query)
					return false;

				return false;
			}

			if (uriFormat == ToStringUnescape && !IriParsing) {
				if (uriKind == UriKind.Relative)
					return false;

				if ("$&+,;=@".Contains (cStr))
					return true;

				if (c < 0x20 || c == 0x7f)
					return true;
			}

			if (uriFormat == UriFormat.SafeUnescaped || uriFormat == ToStringUnescape) {
				if ("-._~".Contains (cStr))
					return true;

				if (" !\"'()*:<>[]^`{}|".Contains (cStr))
					return uriKind != UriKind.Relative;

				if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
					return true;

				if (c > 0x7f)
					return true;

				return false;
			}

			if (uriFormat == UriFormat.UriEscaped) {
				if (!IriParsing) {
					if (".".Contains (cStr)) {
						if (SchemeContains (scheme, UriSchemes.File))
							return component != UriComponents.Fragment;

						return component != UriComponents.Query && component != UriComponents.Fragment &&
							   SchemeContains (scheme, sDecoders);
					}

					return false;
				}

				if ("-._~".Contains (cStr))
					return true;

				if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
					return true;

				return false;
			}

			return false;
		}

		private static bool NeedToEscape (char c, UriSchemes scheme, UriComponents component, UriKind uriKind,
			UriFormat uriFormat)
		{
			string cStr = c.ToString (CultureInfo.InvariantCulture);

			if (c == '?') {
				if (uriFormat == UriFormat.Unescaped)
					return false;

				if (!SupportsQuery (scheme))
					return component != UriComponents.Fragment;

				//Avoid removing query
				if (component == UriComponents.Path)
					return false;

				return false;
			}

			if (c == '#') {
				//Avoid removing fragment
				if (component == UriComponents.Path || component == UriComponents.Query)
					return false;

				return !IriParsing && uriFormat == UriFormat.UriEscaped;
			}

			if (uriFormat == UriFormat.SafeUnescaped || uriFormat == ToStringUnescape) {
				if ("%".Contains (cStr))
					return uriKind != UriKind.Relative;
			}

			if (uriFormat == UriFormat.SafeUnescaped) {
				if (c < 0x20 || c == 0x7F)
					return true;
			}

			if (uriFormat == UriFormat.UriEscaped) {
				if (c < 0x20 || c >= 0x7F)
					return true;

				if (" \"%<>^`{}|".Contains (cStr))
					return true;

				if ("[]".Contains (cStr))
					return !IriParsing;

				if (c == '\\') {
					return component != UriComponents.Path ||
						   SchemeContains (scheme,
							   UriSchemes.Gopher | UriSchemes.Ldap | UriSchemes.Mailto | UriSchemes.Nntp |
							   UriSchemes.Telnet);
				}
			}

			return false;
		}
	}
}
