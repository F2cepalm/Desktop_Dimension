using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace InfiniteSpace.Rendering;

internal static class NoteParagraphs
{
	private const int LinesPerPiece = 40;

	private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras(EmphasisExtraOptions.Strikethrough).UseTaskLists().UseAutoLinks()
		.DisableHtml()
		.Build();

	public static List<NoteParagraph> Plain(string text)
	{
		List<NoteParagraph> list = new List<NoteParagraph>();
		string[] array = text.Split('\n');
		for (int i = 0; i < array.Length; i += 40)
		{
			string text2 = string.Join('\n', array, i, Math.Min(40, array.Length - i));
			list.Add(new NoteParagraph(ParagraphKind.Text, text2, Array.Empty<StyledRun>(), 0, 0, 0, null, i > 0));
		}
		return list;
	}

	public static List<NoteParagraph> Markdown(string text)
	{
		List<NoteParagraph> list = new List<NoteParagraph>();
		foreach (Block item in Markdig.Markdown.Parse(text, Pipeline))
		{
			AddBlock(list, item, 0, 0, null, tight: false);
		}
		return list;
	}

	public static bool IsSafeLink(string? url)
	{
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri result))
		{
			if (!(result.Scheme == Uri.UriSchemeHttp))
			{
				return result.Scheme == Uri.UriSchemeHttps;
			}
			return true;
		}
		return false;
	}

	private static void AddBlock(List<NoteParagraph> paragraphs, Block block, int indent, int quote, string? marker, bool tight)
	{
		if (!(block is HeadingBlock headingBlock))
		{
			if (!(block is ParagraphBlock paragraphBlock))
			{
				if (!(block is FencedCodeBlock) && !(block is CodeBlock))
				{
					if (!(block is ThematicBreakBlock))
					{
						if (block is QuoteBlock quoteBlock)
						{
							bool flag = true;
							{
								foreach (Block item in quoteBlock)
								{
									AddBlock(paragraphs, item, indent, quote + 1, flag ? marker : null, flag && tight);
									flag = false;
								}
								return;
							}
						}
						if (block is ListBlock listBlock)
						{
							int num = ((!int.TryParse(listBlock.OrderedStart, out var result)) ? 1 : result);
							bool flag2 = true;
							{
								foreach (ListItemBlock item2 in listBlock.OfType<ListItemBlock>())
								{
									string text = ((!listBlock.IsOrdered) ? (indent switch
									{
										0 => "•", 
										1 => "◦", 
										_ => "▪", 
									}) : $"{num++}.");
									string text2 = text;
									bool flag3 = true;
									foreach (Block item3 in item2)
									{
										bool tight2 = ((flag2 & flag3) ? tight : (!listBlock.IsLoose));
										AddBlock(paragraphs, item3, indent + 1, quote, flag3 ? text2 : null, tight2);
										flag3 = false;
									}
									flag2 = false;
								}
								return;
							}
						}
						if (!(block is HtmlBlock htmlBlock))
						{
							if (block is ContainerBlock containerBlock)
							{
								foreach (Block item4 in containerBlock)
								{
									AddBlock(paragraphs, item4, indent, quote, marker, tight);
								}
								return;
							}
							if (block is LeafBlock { Inline: not null } leafBlock)
							{
								var (text3, runs) = Inlines(leafBlock.Inline);
								paragraphs.Add(new NoteParagraph(ParagraphKind.Text, text3, runs, 0, indent, quote, marker, tight));
							}
						}
						else
						{
							paragraphs.Add(new NoteParagraph(ParagraphKind.Text, htmlBlock.Lines.ToString(), Array.Empty<StyledRun>(), 0, indent, quote, marker, tight));
						}
					}
					else
					{
						paragraphs.Add(new NoteParagraph(ParagraphKind.Rule, "", Array.Empty<StyledRun>(), 0, indent, quote, marker, tight));
					}
				}
				else
				{
					string text4 = ((LeafBlock)block).Lines.ToString().TrimEnd(new char[2] { '\n', '\r' }).Replace("\r\n", "\n");
					paragraphs.Add(new NoteParagraph(ParagraphKind.Code, text4, Array.Empty<StyledRun>(), 0, indent, quote, marker, tight));
				}
			}
			else
			{
				var (text5, runs2) = Inlines(paragraphBlock.Inline);
				if (paragraphBlock.Inline?.FirstChild is TaskList taskList && marker != null)
				{
					marker = (taskList.Checked ? "☑" : "☐");
				}
				paragraphs.Add(new NoteParagraph(ParagraphKind.Text, text5.TrimStart(), ShiftRuns(runs2, text5.Length - text5.TrimStart().Length), 0, indent, quote, marker, tight));
			}
		}
		else
		{
			var (text6, runs3) = Inlines(headingBlock.Inline);
			paragraphs.Add(new NoteParagraph(ParagraphKind.Heading, text6, runs3, headingBlock.Level, indent, quote, marker, tight));
		}
	}

	private static List<StyledRun> ShiftRuns(List<StyledRun> runs, int by)
	{
		if (by == 0)
		{
			return runs;
		}
		List<StyledRun> list = new List<StyledRun>(runs.Count);
		foreach (StyledRun run in runs)
		{
			int num = Math.Max(0, run.Start - by);
			int num2 = Math.Max(num, run.Start + run.Length - by);
			if (num2 > num)
			{
				list.Add(run with
				{
					Start = num,
					Length = num2 - num
				});
			}
		}
		return list;
	}

	private static (string Text, List<StyledRun> Runs) Inlines(ContainerInline? inlines)
	{
		StringBuilder stringBuilder = new StringBuilder();
		List<StyledRun> list = new List<StyledRun>();
		if (inlines != null)
		{
			AddInlines(inlines, stringBuilder, list, TextStyle.None, null);
		}
		return (Text: stringBuilder.ToString(), Runs: list);
	}

	private static void AddInlines(ContainerInline container, StringBuilder text, List<StyledRun> runs, TextStyle style, string? url)
	{
		foreach (Inline item in container)
		{
			int length = text.Length;
			if (item is TaskList)
			{
				continue;
			}
			if (!(item is LiteralInline literalInline))
			{
				if (!(item is CodeInline codeInline))
				{
					if (!(item is LineBreakInline lineBreakInline))
					{
						if (!(item is HtmlEntityInline htmlEntityInline))
						{
							if (!(item is HtmlInline htmlInline))
							{
								if (!(item is AutolinkInline autolinkInline))
								{
									if (item is LinkInline linkInline)
									{
										if (linkInline.IsImage)
										{
											string text2 = PlainText(linkInline);
											StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, text);
											handler.AppendLiteral("[image");
											handler.AppendFormatted((text2.Length > 0) ? (": " + text2) : "");
											handler.AppendLiteral("]");
											text.Append(ref handler);
											runs.Add(new StyledRun(length, text.Length - length, style | TextStyle.Italic, url));
											continue;
										}
										LinkInline linkInline2 = linkInline;
										string url2 = (IsSafeLink(linkInline2.Url) ? linkInline2.Url : null);
										AddInlines(linkInline2, text, runs, style | TextStyle.Link, url2);
										if (linkInline2.FirstChild == null)
										{
											string url3 = linkInline2.Url;
											if (url3 != null && url3.Length > 0)
											{
												text.Append(url3);
											}
										}
										if (text.Length > length)
										{
											runs.Add(new StyledRun(length, text.Length - length, style | TextStyle.Link, url2));
										}
									}
									else if (!(item is EmphasisInline emphasisInline))
									{
										if (item is ContainerInline container2)
										{
											AddInlines(container2, text, runs, style, url);
										}
									}
									else
									{
										TextStyle textStyle;
										if (emphasisInline.DelimiterChar == '~')
										{
											textStyle = TextStyle.Strike;
										}
										else
										{
											textStyle = ((emphasisInline.DelimiterCount >= 3) ? (TextStyle.Bold | TextStyle.Italic) : ((emphasisInline.DelimiterCount == 2) ? TextStyle.Bold : TextStyle.Italic));
										}
										AddInlines(emphasisInline, text, runs, style | textStyle, url);
										if (text.Length > length)
										{
											runs.Add(new StyledRun(length, text.Length - length, style | textStyle, url));
										}
									}
								}
								else
								{
									text.Append(autolinkInline.Url);
									runs.Add(new StyledRun(length, autolinkInline.Url.Length, style | TextStyle.Link, IsSafeLink(autolinkInline.Url) ? autolinkInline.Url : null));
								}
							}
							else
							{
								text.Append(htmlInline.Tag);
							}
						}
						else
						{
							text.Append(htmlEntityInline.Transcoded.ToString());
						}
					}
					else
					{
						text.Append(lineBreakInline.IsHard ? '\n' : ' ');
					}
				}
				else
				{
					text.Append(codeInline.Content);
					runs.Add(new StyledRun(length, codeInline.Content.Length, style | TextStyle.Code, url));
				}
			}
			else
			{
				text.Append(literalInline.Content.ToString());
			}
		}
	}

	private static string PlainText(ContainerInline container)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (LiteralInline item in container.Descendants<LiteralInline>())
		{
			stringBuilder.Append(item.Content.ToString());
		}
		return stringBuilder.ToString();
	}
}
