using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.Editor;
using UnityEngine;

namespace Starbind.Tests
{
    public class ChapterRouteTests
    {
        static readonly Vector3Int[] Steps = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };

        [TestCase("forest"), TestCase("mine"), TestCase("lake"), TestCase("observatory"), TestCase("citadel")]
        public void WalkEveryRoomAndTraverseBothDoors(string id)
        {
            var map = ScriptableObject.CreateInstance<ChapterExplorer>();
            try
            {
                map.LoadLayout(id, false);
                var chapter = ChapterExplorer.ReadLayouts().Single(c => c.id == id);
                var start = map.Position;
                var queue = new Queue<Vector3Int>();
                var parent = new Dictionary<Vector3Int, Vector3Int> { { start, start } };
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    foreach (var step in Steps)
                    {
                        var next = at + step;
                        if (!parent.ContainsKey(next) && map.IsWalkable(next))
                        {
                            parent[next] = at;
                            queue.Enqueue(next);
                        }
                    }
                }
                Assert.AreEqual(chapter.rows.Sum(row => row.Count(c => c == '.')), parent.Count);
                Assert.IsTrue(map.IsWall(Vector3Int.zero));
                foreach (var anchor in chapter.anchors)
                {
                    var at = new Vector3Int(anchor.x, anchor.y, 0);
                    var path = new Stack<Vector3Int>();
                    while (at != start) { path.Push(at - parent[at]); at = parent[at]; }
                    map.LoadLayout(id, false);
                    foreach (var step in path) Assert.IsTrue(map.Move(step));
                    Assert.AreEqual(new Vector3Int(anchor.x, anchor.y, 0), map.Position);
                }
                map.LoadLayout(id, true);
                var exit = map.Position;
                Assert.AreEqual(chapter.next != "", map.Travel());
                if (chapter.next != "")
                {
                    Assert.AreEqual(chapter.next, map.CurrentId);
                    Assert.IsTrue(map.Travel());
                    Assert.AreEqual(id, map.CurrentId);
                    Assert.AreEqual(exit, map.Position);
                }
                var texture = (RenderTexture)map.RenderMap(new Rect(0, 0, 512, 512));
                var previous = RenderTexture.active;
                // read the whole target: on a Retina display the preview texture is 2x the requested rect,
                // and reading only 512x512 saw one corner of the map (the old forest/lake failures)
                var frame = new Texture2D(texture.width, texture.height);
                try
                {
                    RenderTexture.active = texture;
                    frame.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    frame.Apply();
                    Assert.Greater(frame.GetPixels32().Distinct().Count(), 4, "tilemap and anchors must render");
                    var dir = Path.Combine(Application.dataPath, "../TestResults/graybox");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, id + ".png"), frame.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(frame); }
            }
            finally { Object.DestroyImmediate(map); }
        }
    }
}
