/******************************************************************************
 * Spine Runtimes Software License
 * Version 2.3
 * 
 * Copyright (c) 2013-2015, Esoteric Software
 * All rights reserved.
 * 
 * You are granted a perpetual, non-exclusive, non-sublicensable and
 * non-transferable license to use, install, execute and perform the Spine
 * Runtimes Software (the "Software") and derivative works solely for personal
 * or internal use. Without the written permission of Esoteric Software (see
 * Section 2 of the Spine Software License Agreement), you may not (a) modify,
 * translate, adapt or otherwise create derivative works, improvements of the
 * Software or develop new applications using the Software or (b) remove,
 * delete, alter or obscure any trademarks or any copyright, trademark, patent
 * or other intellectual property or proprietary rights notices on or in the
 * Software, including any copy thereof. Redistributions in binary or source
 * form must include this license and terms.
 * 
 * THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL ESOTERIC SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
 * OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

namespace Spine {
	public static class Json {

		public static object Deserialize (byte[] text) {

			#if UNITY_EDITOR
			long b = GC.GetTotalMemory (true);
			#endif

			var parser = new SharpJson.JsonDecoder ();
			parser.parseNumbersAsFloat = true;
			object jsonContent = parser.DecodeBytes (text);

			#if UNITY_EDITOR
			long a = GC.GetTotalMemory (true);
			UnityEngine.Debug.Log (string.Format ("JSON memory usage: {0} MB", (a - b) / 1024.0f / 1024.0f));
			#endif

			return jsonContent;
		}

		public static object Deserialize (TextReader text) {
			var parser = new SharpJson.JsonDecoder();
			parser.parseNumbersAsFloat = true;
			return parser.Decode(text.ReadToEnd());
		}
	}
}

/**
 *
 * Copyright (c) 2016 Adriano Tinoco d'Oliveira Rezende
 * 
 * Based on the JSON parser by Patrick van Bergen
 * http://techblog.procurios.nl/k/news/view/14605/14863/how-do-i-write-my-own-parser-(for-json).html
 *
 * Changes made:
 * 
 * 	- Optimized parser speed (deserialize roughly near 3x faster than original)
 *  - Added support to handle lexer/parser error messages with line numbers
 *  - Added more fine grained control over type conversions during the parsing
 *  - Refactory API (Separate Lexer code from Parser code and the Encoder from Decoder)
 *
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial
 * portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
 * OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 */
namespace SharpJson
{
	class Lexer
	{
		private Dictionary<string,string> duplicatedStrings = new Dictionary<string, string>();
		private Dictionary<float,object> duplicatedFloats = new Dictionary<float, object>();

		public enum Token {
			None,
			Null,
			True,
			False,
			Colon,
			Comma,
			String,
			Number,
			CurlyOpen,
			CurlyClose,
			SquaredOpen,
			SquaredClose,
		};

		public bool hasError {
			get {
				return !success;
			}
		}

		public int lineNumber {
			get;
			private set;
		}

		public bool parseNumbersAsFloat {
			get;
			set;
		}

		byte[] json;
		int index = 0;
		bool success = true;
		char[] stringBuffer = new char[4096];

		public Lexer(byte[] jsonArray)
		{
			Reset();

			json = jsonArray;
			parseNumbersAsFloat = false;
		}

		public Lexer(string text)
		{
			Reset();

			json = Encoding.ASCII.GetBytes (text);
			parseNumbersAsFloat = false;
		}

		public void Reset()
		{
			index = 0;
			lineNumber = 1;
			success = true;
		}

		public string ParseString()
		{
			int idx = 0;

			SkipWhiteSpaces();


			// special case the mose often repeated strings
			// "time"
			if (json [index + 1] == 't' && 
				json [index + 2] == 'i' && 
				json [index + 3] == 'm' && 
				json [index + 4] == 'e' &&
				json [index + 5] == '"') {
				index += 6;
				return "time";
			}

			// "name"
			if (json [index + 1] == 'n' && 
				json [index + 2] == 'a' && 
				json [index + 3] == 'm' && 
				json [index + 4] == 'e' &&
				json [index + 5] == '"') {
				index += 6;
				return "name";
			}

			// "hull"
			if (json [index + 1] == 'h' && 
				json [index + 2] == 'u' && 
				json [index + 3] == 'l' && 
				json [index + 4] == 'l' &&
				json [index + 5] == '"') {
				index += 6;
				return "hull";
			}

			// "type"
			if (json [index + 1] == 't' && 
				json [index + 2] == 'y' && 
				json [index + 3] == 'p' && 
				json [index + 4] == 'e' &&
				json [index + 5] == '"') {
				index += 6;
				return "type";
			}

			// "width"
			if (json [index + 1] == 'w' && 
				json [index + 2] == 'i' && 
				json [index + 3] == 'd' && 
				json [index + 4] == 't' &&
				json [index + 5] == 'h' &&
				json [index + 6] == '"') {
				index += 7;
				return "width";
			}

			// "height"
			if (json [index + 1] == 'h' && 
				json [index + 2] == 'e' && 
				json [index + 3] == 'i' && 
				json [index + 4] == 'g' &&
				json [index + 5] == 'h' &&
				json [index + 6] == 't' &&
				json [index + 7] == '"') {
				index += 8;
				return "height";
			}

			// "curve"
			if (json [index + 1] == 'c' && 
				json [index + 2] == 'u' && 
				json [index + 3] == 'r' && 
				json [index + 4] == 'v' &&
				json [index + 5] == 'e' &&
				json [index + 6] == '"') {
				index += 7;
				return "curve";
			}

			// "angle"
			if (json [index + 1] == 'a' && 
				json [index + 2] == 'n' && 
				json [index + 3] == 'g' && 
				json [index + 4] == 'l' &&
				json [index + 5] == 'e' &&
				json [index + 6] == '"') {
				index += 7;
				return "angle";
			}

			// "rotate"
			if (json [index + 1] == 'r' && 
				json [index + 2] == 'o' && 
				json [index + 3] == 't' && 
				json [index + 4] == 'a' &&
				json [index + 5] == 't' &&
				json [index + 6] == 'e' &&
				json [index + 7] == '"') {
				index += 8;
				return "rotate";
			}

			// "scale"
			if (json [index + 1] == 's' && 
				json [index + 2] == 'c' && 
				json [index + 3] == 'a' && 
				json [index + 4] == 'l' &&
				json [index + 5] == 'e' &&
				json [index + 6] == '"') {
				index += 7;
				return "scale";
			}

			// "translate"
			if (json [index + 1] == 't' && 
				json [index + 2] == 'r' && 
				json [index + 3] == 'a' && 
				json [index + 4] == 'n' &&
				json [index + 5] == 's' &&
				json [index + 6] == 'l' &&
				json [index + 7] == 'a' &&
				json [index + 8] == 't' &&
				json [index + 9] == 'e' &&
				json [index + 10] == '"') {
				index += 11;
				return "translate";
			}

			// "stepped"
			if (json [index + 1] == 's' && 
				json [index + 2] == 't' && 
				json [index + 3] == 'e' && 
				json [index + 4] == 'p' &&
				json [index + 5] == 'p' &&
				json [index + 6] == 'e' &&
				json [index + 7] == 'd' &&
				json [index + 8] == '"') {
				index += 9;
				return "stepped";
			}

			// "attachment"
			if (json [index + 1] == 'a' && 
				json [index + 2] == 't' && 
				json [index + 3] == 't' && 
				json [index + 4] == 'a' &&
				json [index + 5] == 'c' &&
				json [index + 6] == 'h' &&
				json [index + 7] == 'm' &&
				json [index + 8] == 'e' &&
				json [index + 9] == 'n' &&
				json [index + 10] == 't' &&
				json [index + 11] == '"') {
				index += 12;
				return "attachment";
			}

			// "x"
			if (json [index + 1] == 'x' && 
				json [index + 2] == '"') {
				index += 3;
				return "x";
			}

			// "y"
			if (json [index + 1] == 'y' && 
				json [index + 2] == '"') {
				index += 3;
				return "y";
			}

			// "z"
			if (json [index + 1] == 'z' && 
				json [index + 2] == '"') {
				index += 3;
				return "z";
			}




			
			// "
			char c = (char)json[index++];
			
			bool failed = false;
			bool complete = false;
			
			while (!complete && !failed) {
				if (index == json.Length)
					break;
				
				c = (char)json[index++];
				if (c == '"') {
					complete = true;
					break;
				} else if (c == '\\') {
					if (index == json.Length)
						break;
					
					c = (char)json[index++];
					
					switch (c) {
					case '"':
						stringBuffer[idx++] = '"';
						break;
					case '\\':
						stringBuffer[idx++] = '\\';
						break;
					case '/':
						stringBuffer[idx++] = '/';
						break;
					case 'b':
						stringBuffer[idx++] = '\b';
						break;
					case'f':
							stringBuffer[idx++] = '\f';
						break;
					case 'n':
						stringBuffer[idx++] = '\n';
						break;
					case 'r':
						stringBuffer[idx++] = '\r';
						break;
					case 't':
						stringBuffer[idx++] = '\t';
						break;
					case 'u':
						int remainingLength = json.Length - index;
						if (remainingLength >= 4) {
							//var hex = new string(json, index, 4);
							
							// XXX: handle UTF
							//stringBuffer[idx++] = (char) Convert.ToInt32(hex, 16);
							
							// skip 4 chars
							index += 4;
						} else {
							failed = true;
						}
						break;
					}
				} else {
					stringBuffer[idx++] = c;
				}
			}
			
			if (!complete) {
				success = false;
				return null;
			}

			string retString = new string (stringBuffer, 0, idx);
			if (duplicatedStrings.ContainsKey (retString)) {
				return duplicatedStrings[retString];
			}

			duplicatedStrings [retString] = retString;
			return retString;
		}

		public object ParseFloatNumber()
		{
			// reduce boxing of floats by detecting duplicated float objects
			float f;

			if (json [index + 0] == '0' && json [index + 1] == ',') {
				index += 2;
				f = 0.0f;
			} else if (json [index + 0] == '1' && json [index + 1] == ',') {
				index += 2;
				f = 1.0f;
			} else {
				f = FastParse.atof (json, ref index);
			}

			if (duplicatedFloats.ContainsKey (f)) {
				return duplicatedFloats[f];
			}
			duplicatedFloats [f] = f;
			return duplicatedFloats [f];
		}

		public double ParseDoubleNumber()
		{
			if (json [index + 0] == '0' && json [index + 1] == ',') {
				index += 2;
				return 0;
			}
			if (json [index + 0] == '1' && json [index + 1] == ',') {
				index += 2;
				return 1;
			}
			return FastParse.atof (json, ref index);
		}
		
		int GetLastIndexOfNumber(int index)
		{
			int lastIndex;
			
			for (lastIndex = index; lastIndex < json.Length; lastIndex++) {
				char ch = (char)json[lastIndex];
				
				if ((ch < '0' || ch > '9') && ch != '+' && ch != '-'
				    && ch != '.' && ch != 'e' && ch != 'E')
					break;
			}
			
			return lastIndex - 1;
		}


		public void SkipThisObject()
		{
			int curlyLevel = 0;

			// find the first '{'
			for (; index < json.Length; index++) {
				char ch = (char)json [index];
				if (ch == '{' || ch == ']') {
					curlyLevel++;
					index++;
					break;
				}
			}

			for (; index < json.Length; index++) {
				char ch = (char)json[index];

				if (ch == '{' || ch == '[')
					curlyLevel++;

				if (ch == '}' || ch == ']')
					curlyLevel--;
				
				if (curlyLevel <= 0) {
					index++;
					break;
				}
			}
		}

		public int EstimateNumberOfObjectsInThisTable()
		{
			int curlyLevel = 0;
			int numObjects = 1;

			int tempIndex = index+1;
			for (; tempIndex < json.Length; tempIndex++) {
				char ch = (char)json[tempIndex];

				if (ch == '{' || ch == '[')
					curlyLevel++;

				if (ch == '}' || ch == ']')
					curlyLevel--;

				if (curlyLevel == 0) {
					if (ch == ',')
						numObjects++;
				}

				if (curlyLevel < 0)
					break;
			}

			return numObjects;
		}

		void SkipWhiteSpaces()
		{
			for (; index < json.Length; index++) {
				char ch = (char)json[index];

				if (ch == '\n')
					lineNumber++;

				if (!char.IsWhiteSpace((char)json[index]))
					break;
			}
		}

		public Token LookAhead()
		{
			SkipWhiteSpaces();

			int savedIndex = index;
			return NextToken(json, ref savedIndex);
		}

		public Token NextToken()
		{
			SkipWhiteSpaces();
			return NextToken(json, ref index);
		}

		static Token NextToken(byte[] json, ref int index)
		{
			if (index == json.Length)
				return Token.None;
			
			char c = (char)json[index++];
			
			switch (c) {
			case '{':
				return Token.CurlyOpen;
			case '}':
				return Token.CurlyClose;
			case '[':
				return Token.SquaredOpen;
			case ']':
				return Token.SquaredClose;
			case ',':
				return Token.Comma;
			case '"':
				return Token.String;
			case '0': case '1': case '2': case '3': case '4':
			case '5': case '6': case '7': case '8': case '9':
			case '-':
				return Token.Number;
			case ':':
				return Token.Colon;
			}

			index--;
			
			int remainingLength = json.Length - index;
			
			// false
			if (remainingLength >= 5) {
				if (json[index] == 'f' &&
				    json[index + 1] == 'a' &&
				    json[index + 2] == 'l' &&
				    json[index + 3] == 's' &&
				    json[index + 4] == 'e') {
					index += 5;
					return Token.False;
				}
			}
			
			// true
			if (remainingLength >= 4) {
				if (json[index] == 't' &&
				    json[index + 1] == 'r' &&
				    json[index + 2] == 'u' &&
				    json[index + 3] == 'e') {
					index += 4;
					return Token.True;
				}
			}
			
			// null
			if (remainingLength >= 4) {
				if (json[index] == 'n' &&
				    json[index + 1] == 'u' &&
				    json[index + 2] == 'l' &&
				    json[index + 3] == 'l') {
					index += 4;
					return Token.Null;
				}
			}

			return Token.None;
		}
	}

	public class JsonDecoder
	{
		public string errorMessage {
			get;
			private set;
		}

		public bool parseNumbersAsFloat {
			get;
			set;
		}

		Lexer lexer;

		public JsonDecoder()
		{
			errorMessage = null;
			parseNumbersAsFloat = false;
		}

		public object DecodeBytes(byte[] jsonArray)
		{
			errorMessage = null;

			lexer = new Lexer(jsonArray);
			lexer.parseNumbersAsFloat = parseNumbersAsFloat;

			return ParseValue();
		}

		public object Decode(string text)
		{
			errorMessage = null;

			lexer = new Lexer(text);
			lexer.parseNumbersAsFloat = parseNumbersAsFloat;

			return ParseValue();
		}

		public static object DecodeText(string text)
		{
			var builder = new JsonDecoder();
			return builder.Decode(text);
		}

		IDictionary<string, object> ParseObject()
		{
			var table = new FastParse.MemoryLiteDictionary<string, object>();

			// Estimate the number of objects that are going to go in this dictionary
			table.SetCapacity (lexer.EstimateNumberOfObjectsInThisTable ());

			// {
			lexer.NextToken();

			while (true) {
				var token = lexer.LookAhead();

				switch (token) {
				case Lexer.Token.None:
					TriggerError("Invalid token");
					return null;
				case Lexer.Token.Comma:
					lexer.NextToken();
					break;
				case Lexer.Token.CurlyClose:
					lexer.NextToken();
					return table;
				default:
					// name
					string name = lexer.ParseString ();
					/*
					if (name.Contains ("TEST_") || name.Contains ("test_")) {
						// If an artists labels an animation as test, let's not include it and save some memory
						lexer.SkipThisObject();
						break;
					}*/

					if (errorMessage != null)
						return null;

					// :
					token = lexer.NextToken ();

					if (token != Lexer.Token.Colon) {
						TriggerError ("Invalid token; expected ':'");
						return null;
					}
					
					// value
					object value = ParseValue ();

					if (errorMessage != null)
						return null;

					if (value != null) {
						table [name] = value;
					}
					break;
				}
			}
			
			//return null; // Unreachable code
		}

		IList<object> ParseArray()
		{
			var array = new FastParse.MemoryLiteList<object> ();
			array.SetCapacity (lexer.EstimateNumberOfObjectsInThisTable ());
			
			// [
			lexer.NextToken();

			while (true) {
				var token = lexer.LookAhead();

				switch (token) {
				case Lexer.Token.None:
					TriggerError("Invalid token");
					return null;
				case Lexer.Token.Comma:
					lexer.NextToken();
					break;
				case Lexer.Token.SquaredClose:
					lexer.NextToken();
					return array;
				default:
					object value = ParseValue();

					if (errorMessage != null)
						return null;

					array.Add(value);
					break;
				}
			}
			
			//return null; // Unreachable code
		}

		object ParseValue()
		{
			switch (lexer.LookAhead()) {
			case Lexer.Token.String:
				return lexer.ParseString();
			case Lexer.Token.Number:
				if (parseNumbersAsFloat) {
					return lexer.ParseFloatNumber ();
				} else {
					return lexer.ParseDoubleNumber ();
				}
			case Lexer.Token.CurlyOpen:
				return ParseObject();
			case Lexer.Token.SquaredOpen:
				return ParseArray();
			case Lexer.Token.True:
				lexer.NextToken();
				return true;
			case Lexer.Token.False:
				lexer.NextToken();
				return false;
			case Lexer.Token.Null:
				lexer.NextToken();
				return null;
			case Lexer.Token.None:
				break;
			}

			TriggerError("Unable to parse value");
			return null;
		}

		void TriggerError(string message)
		{
			errorMessage = string.Format("Error: '{0}' at line {1}",
			                             message, lexer.lineNumber);
		}

	}
}
