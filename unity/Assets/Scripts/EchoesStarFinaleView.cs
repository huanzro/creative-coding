using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesView
    {
        private StarFinaleArtwork finaleArtwork;
        private StarKeepsakeShare finaleShare;
        private GUIStyle finaleTitle, finaleCaption;
        private int finalePaper = -1;
        private bool finaleExported;
        private const float FinaleWorldEnd = 3.6f, KeepsakeEntrance = 12.0f;
        public float StarFinaleBrightness { get; set; } = 1.12f;

        private void UpdateStarFinale(Journey journey)
        {
            if (finalePaper != journey.StarPaper || !journey.StarFinaleActive)
            {
                if (finaleArtwork != null || finaleShare != null) DisposeStarFinale();
                finalePaper = journey.StarPaper;
                finaleExported = false;
            }
            starInk.Frame.finaleAge = journey.StarFinaleAge;
            if (!journey.StarFinaleActive || (starInk.Frame.archive?.Count ?? 0) == 0 ||
                starInk.Frame.depth < Journey.StarFinaleDistance - .001f) return;
            if (finaleArtwork == null && journey.StarFinaleAge >= 1.15f)
                finaleArtwork = new StarFinaleArtwork(starInk.Frame);
            if (finaleExported || journey.StarFinaleAge < KeepsakeEntrance || finaleArtwork?.Ready != true) return;
            finaleExported = true;
            byte[] image = finaleArtwork.MakePng(starInk.Frame);
            if (image != null) finaleShare = new StarKeepsakeShare(image);
        }

        private void DrawStarFinale(Journey journey, bool split)
        {
            float age = journey.StarFinaleAge;
            bool repaint = Event.current.type == EventType.Repaint;
            var stage = new Rect(0,0,Width,Height);
            if (repaint)
            {
                if (world?.Ready == true)
                    Paint(stage, Color.white, world.Texture);
                if (!split && age < FinaleWorldEnd)
                {
                    StarInkFrame ink = starInk.Frame;
                    Vector2 at = Vector2.Scale(StarProjection.StagePoint(ink.cursor - ink.Eye), stage.size);
                    DrawFinaleFingertip(at, age, 1);
                }
                if (finaleArtwork?.Ready == true && age > 1.75f)
                {
                    Texture shot = finaleArtwork.Draw(starInk.Frame, age, StarFinaleBrightness);
                    Paint(stage, new Color(1,1,1,OpeningStoryboard.Ease(1.75f, FinaleWorldEnd, age)), shot);
                }
            }
            float titleVisibility = OpeningStoryboard.Ease(9.45f,11.55f,age);
            if (titleVisibility > .001f)
            {
                PrepareInterface();
                finaleTitle ??= new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 50, fontStyle = FontStyle.Normal };
                float lift = 9 * (1 - titleVisibility);
                Color before = GUI.contentColor;
                GUI.contentColor = new Color(1,.90f,.64f,titleVisibility);
                GUI.Label(new Rect(190, 26 + lift, Width - 380, 66), EchoesLocale.GameTitle, finaleTitle);
                GUI.contentColor = before;
            }
            if (age < KeepsakeEntrance || finaleShare == null) return;
            PrepareInterface();
            finaleCaption ??= new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            if (finaleShare.Qr != null)
            {
                const float size = 148;
                var qr = new Rect(Width - 174,Height - 194,size,size);
                Paint(qr, Color.white, finaleShare.Qr);
                GUI.Label(new Rect(qr.x - 38, qr.yMax + 3,size + 76,34),
                    EchoesLocale.T("同一网络扫码保存", "Scan on the same network"), finaleCaption);
            }
            if (finaleShare.LocalPath != null || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                string download = Application.platform == RuntimePlatform.WebGLPlayer
                    ? EchoesLocale.T("下载光迹 PNG","Download artwork PNG")
                    : EchoesLocale.T("打开保存位置","Open saved artwork folder");
                if (GUI.Button(new Rect(29,Height - 63,225,31),download,button)) finaleShare.Open();
            }
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(.91f,.78f,.55f,.82f);
            GUI.Label(new Rect(29,Height - 32,225,25), EchoesLocale.T("R 重新绘制","R · Draw again"),body);
            GUI.contentColor = old;
        }

        private void DrawFinaleFingertip(Vector2 at, float age, float size)
        {
            if (age < 0 || age >= FinaleWorldEnd) return;
            float bloom = OpeningStoryboard.Window(age,.03f,2.12f,2.26f,FinaleWorldEnd);
            var gold = new Color(1,.73f,.36f);
            Orb(at.x,at.y,(34 + 172 * bloom) * size,gold,.87f * bloom);
            Orb(at.x,at.y,(9 + 18 * bloom) * size,new Color(1,.94f,.72f),bloom);
            Ripple(at.x,at.y,(25 + 148 * bloom) * size,gold,.25f * bloom);
        }

        private void DisposeStarFinale()
        {
            finaleArtwork?.Dispose();
            finaleArtwork = null;
            finaleShare?.Dispose();
            finaleShare = null;
        }
    }
}
