using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;
using Harmony;
using UnityEngine;
using Error = BestHTTP.SocketIO.Error;

namespace Galaxy_at_War
{
    public static class DynamicLogos
    {
        public static void PlaceAndScaleLogos(Dictionary<string, string> logoNames, StarmapRenderer renderer)
        {
            var boundingRects = new Dictionary<FactionValue, BoundingRect>();
            var logos = new Dictionary<FactionValue, GameObject>();
            foreach (string faction in logoNames.Keys)
            {
                logos.Add(Core.FactionValues.Find(x => x.Name == faction), GameObject.Find(logoNames[faction]));
            }
            
            foreach (var starNode in renderer.starmap.VisisbleSystem)
            {
                var faction = starNode.System.OwnerValue;
                if (!logos.ContainsKey(faction))
                    continue;

                BoundingRect mapData;
                if (boundingRects.ContainsKey(faction))
                {
                    mapData = boundingRects[faction];
                }
                else
                {
                    mapData = new BoundingRect();
                    boundingRects.Add(faction, mapData);
                }

                mapData.MinX = Mathf.Min(mapData.MinX, starNode.NormalizedPosition.x);
                mapData.MaxX = Mathf.Max(mapData.MaxX, starNode.NormalizedPosition.x);
                mapData.MinY = Mathf.Min(mapData.MinY, starNode.NormalizedPosition.y);
                mapData.MaxY = Mathf.Max(mapData.MaxY, starNode.NormalizedPosition.y);
            }

            foreach (var faction in logos.Keys)
            {
                if (!boundingRects.ContainsKey(faction))
                    continue;

                var logo = logos[faction];

                var boundingRect = boundingRects[faction];
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (boundingRect.MinX == float.MaxValue)
                {
                    // there were no star systems
                    logo.SetActive(false);
                    continue;
                }

                logo.SetActive(true);

                // position is in the middle of the boundingRect
                var x = (boundingRect.MaxX + boundingRect.MinX) / 2f;
                var y = (boundingRect.MaxY + boundingRect.MinY) / 2f;
                var normalizedPos = new Vector2(x, y);
                logo.transform.position = StarmapRenderer.NormalizeToMapSpace(normalizedPos);

                // scale is based off of the width/height of the boundingRect
                var topRight = StarmapRenderer.NormalizeToMapSpace(new Vector2(boundingRect.MaxX, boundingRect.MaxY));
                var bottomLeft = StarmapRenderer.NormalizeToMapSpace(new Vector2(boundingRect.MinX, boundingRect.MinY));
                var width = topRight.x - bottomLeft.x;
                var height = topRight.y - bottomLeft.y;

                var scale = Mathf.Min(Mathf.Min(width, height) * Core.Settings.LogoScalar, Core.Settings.LogoMaxSize);
                logo.transform.localScale = new Vector3(scale, scale);
            }
        }

        private class BoundingRect
        {
            public float MinX = float.MaxValue;
            public float MaxX = float.MinValue;
            public float MinY = float.MaxValue;
            public float MaxY = float.MinValue;
        }
    }
}
