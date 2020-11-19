using System;
using System.IO;
using System.Linq;
using HtmlAgilityPack;

namespace StackoverflowChatbot.Relay
{
	internal static class FromStackExtensions
	{
		internal static string ProcessStackMessage(this string message, int roomId, string roomName)
		{
			var baseUri = new Uri($"https://chat.stackoverflow.com/rooms/{roomId}/{Uri.EscapeUriString(roomName)}");
			var document = new HtmlDocument();
			document.LoadHtml(message);
			var rootNode = document.DocumentNode.FirstChild;

			// Handle code-blocks.
			if (rootNode.Name.Equals("pre"))
			{
				var text = HtmlEntity.DeEntitize(document.DocumentNode.InnerText);
				return $"```\r\n{text}\r\n```";
			}

			// Handle onebox-content.
			var classes = rootNode.GetAttributeValue("class", string.Empty).Split(' ').Select(x => x.ToLowerInvariant()).ToHashSet();
			if (classes.Contains("ob-xkcd") || classes.Contains("ob-image"))
			{
				var img = document.DocumentNode.SelectSingleNode("//img");
				var src = HtmlEntity.DeEntitize(img?.GetAttributeValue("src", string.Empty));
				if (Uri.TryCreate(baseUri, src, out var result)) return result.ToString();
			}
			else if (classes.Contains("ob-youtube") || classes.Contains("ob-message"))
			{
				var anchor = document.DocumentNode.SelectSingleNode("//a");
				var href = HtmlEntity.DeEntitize(anchor?.GetAttributeValue("href", string.Empty));
				if (Uri.TryCreate(baseUri, href, out var result)) return result.ToString();
			}

			// Handle (multiline) text.
			using (var writer = new StringWriter())
			{
				ConvertTo(rootNode, writer);
				writer.Flush();
				return writer.ToString();
			}
		}

		private static void ConvertTo(HtmlNode node, TextWriter writer)
		{
			switch (node.NodeType)
			{
				case HtmlNodeType.Element:
					if (node.Name.Equals("p") || node.Name.Equals("br"))
						writer.Write("\r\n");
					if (node.HasChildNodes)
						foreach (var childNode in node.ChildNodes)
							ConvertTo(childNode, writer);
					break;
				case HtmlNodeType.Text:
					if (node.InnerText.Trim().Length == 0) break;
					writer.Write(HtmlEntity.DeEntitize(node.InnerText.Trim()));
					break;
			}
		}
	}
}