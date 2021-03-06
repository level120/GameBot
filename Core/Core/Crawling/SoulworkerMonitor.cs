﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using ChloeBot.Soulworker;
using Discord;
using HtmlAgilityPack;

namespace ChloeBot.Crawling
{
    public class SoulworkerMonitor
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly HtmlDocument _htmlDocument = new HtmlDocument();

        private const int NumOfBoards = 4;
        private const string FilePath = @"db.txt";
        private const int AllocateSize = 50; // list 형 게시글 15개 * 3, event 게시글 5개

        /// <summary>
        /// 최신 글을 확인하고 그 결과를 반환합니다.
        /// </summary>
        /// <returns>최신 글 링크 목록</returns>
        public static ICollection<EmbedBuilder> Run() => GetReplyBuilder().ToList();

        private static IEnumerable<EmbedBuilder> GetReplyBuilder()
        {
            UpdateBoardData();

            foreach (var imageUrl in Board.GetNews())
            {
                string titleString;
                var builder = new EmbedBuilder
                {
                    Color = Color.Orange,
                };

                if (imageUrl.Contains(Board.NoticeBoardName)) titleString = "**[공지사항]**";
                else if (imageUrl.Contains(Board.DetailBoardName)) titleString = "**[업데이트]**";
                else if (imageUrl.Contains(Board.GMBoardName)) titleString = "**[GM매거진]**";
                else
                {
                    titleString = "**[이벤트]**";
                    builder.WithTitle($"{titleString} 새로운 게시글이 올라왔어요!")
                        .WithUrl("http://soulworker.game.onstove.com/Event")
                        .WithImageUrl(imageUrl);

                    yield return builder;
                    continue;
                }

                builder.WithTitle($"{titleString} 새로운 게시글이 올라왔어요!")
                    .WithUrl(imageUrl);

                yield return builder;
            }
        }

        private static async void UpdateBoardData()
        {
            Board.RecoveryItems(FilePath);

            var targetItems = new List<string>(AllocateSize);

            foreach (var boardIdx in Enumerable.Range(0, NumOfBoards))
            {
                var url = SoulworkerKR.Urls[boardIdx];
                var html = await _httpClient.GetStringAsync(url);
                _htmlDocument.LoadHtml(html);

                var structPageInfo = new SoulworkerKR();

                string targetType;
                string targetTypeName;

                if (boardIdx < 2)
                {
                    targetType = "table";
                    targetTypeName = structPageInfo.ClassType[boardIdx].Table;
                }
                else
                {
                    targetType = "ul";
                    targetTypeName = structPageInfo.ClassType[boardIdx].Ul;
                }

                List<string> target;

                var res = _htmlDocument.DocumentNode.Descendants(targetType)
                    .Where(node => node.GetAttributeValue("class", "") == targetTypeName)
                    .ToList();

                if (boardIdx < 2)
                {
                    target = res.FirstOrDefault()
                        ?.Descendants("tbody").FirstOrDefault()
                        ?.Descendants("p")
                        .Where(p => p.GetAttributeValue("class", "").Equals("ellipsis"))
                        .Select(p => SoulworkerKR.PrefixUrl + p.ChildNodes.FirstOrDefault()?.GetAttributeValue("href", ""))
                        .ToList();
                }
                else if (boardIdx == 2)
                {
                    res = _htmlDocument.DocumentNode.Descendants(targetType).ToList();

                    target = res.FirstOrDefault()
                        ?.Descendants("div")
                        .Where(p => p.GetAttributeValue("class", "").Equals("thumb"))
                        .Select(p => @"http:" + p.ChildNodes[1].ChildNodes[0].GetAttributeValue("src", ""))
                        .ToList();
                }
                else
                {
                    target = res.FirstOrDefault()
                        ?.Descendants("div")
                        .Where(p => p.GetAttributeValue("class", "").Equals("t-subject"))
                        .Select(p => SoulworkerKR.PrefixUrl + p.ChildNodes[1].GetAttributeValue("href", ""))
                        .ToList();
                }

                if (target != null && target.Any())
                    targetItems.AddRange(target);
            }

            if (targetItems.Any())
                Board.SetData(targetItems);
        }
    }
}