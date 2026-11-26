using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private Transform rememberedGrove, openingArchive;
        private MeshRenderer[] groveRenderers;

        internal void DrawFilmScenery(bool opening, bool leavingRuin, float age)
        {
            if (opening)
            {
                if (openingArchive == null) BuildOpeningArchive();
                openingArchive.gameObject.SetActive(true);
            }
            else if (openingArchive != null) openingArchive.gameObject.SetActive(false);

            if (leavingRuin && rememberedGrove == null) BuildRememberedGrove();
            if (rememberedGrove == null) return;
            float visibility = leavingRuin ? OpeningStoryboard.Window(age, 2.90f, 4.65f, 7.55f, 9.70f) : 0;
            rememberedGrove.gameObject.SetActive(visibility > .002f);
            if (visibility <= .002f) return;
            foreach (var renderer in groveRenderers)
            {
                renderer.GetPropertyBlock(frameworkProperties);
                frameworkProperties.SetFloat(StageLight, visibility);
                renderer.SetPropertyBlock(frameworkProperties);
            }
        }

        private void BuildRememberedGrove()
        {
            rememberedGrove = Group("Forest between the archive and the Star Sea", frameworkRoot);
            var rocks=Geometry();
            var trunks=Geometry();
            var crowns=Geometry();
            var lamps=Geometry();
            for (int side=-1; side<=1; side+=2)
            {
                for (int index=0; index<8; index++)
                {
                    float z=12.4f+index*1.96f+.56f*side;
                    float edge=4.5f+1.44f*Mathf.Sin(index*1.784f+side)+.14f*index;
                    var ground=new Vector3(side*edge,-.32f,z);
                    rocks.Rock(ground+Vector3.down*.26f,new Vector3(1.75f,.61f,1.41f),
                        new Color(.251f,.322f,.245f), index*1.51f, 4, 10, .26f);
                    Tree(trunks,crowns,ground,5.5f+1.84f*Mathf.Sin(index*2.17f+side),.33f+.08f*(index%4));
                    Fern(crowns,ground+new Vector3(-side*.71f,.17f,-.47f),.48f+.14f*(index%3),index*1.77f);
                    Vector3 gleam=ground+new Vector3(-side*.98f,1.5f+.96f*Mathf.Sin(index*1.59f),-.50f);
                    lamps.Rock(gleam,Vector3.one*(.027f+(index%3)*.019f),Warm,index,4,7,.11f);
                }
            }
            for (int index=0; index<7; index++)
            {
                float z=12.7f+index*1.55f;
                Vector3 step=new Vector3(Mathf.Sin(index*.49f)*.69f,-.50f+index*.07f,z);
                rocks.Rock(step,new Vector3(2.51f,.34f,.93f),new Color(.28f,.31f,.24f),z,4,11,.24f);
            }
            var soilDeck=PlaceStarBackdrop(rememberedGrove,"Grove stepping stones",rocks,stone);
            var trees=PlaceStarBackdrop(rememberedGrove,"Grove roots and trunks",trunks,wood);
            var foliage=PlaceStarBackdrop(rememberedGrove,"Grove canopies and ferns",crowns,leaves);
            var pollen=PlaceStarBackdrop(rememberedGrove,"Amber fireflies lead out of the grove",lamps,glow);
            Light(pollen,Color.white,1.23f);
            groveRenderers=new[] { soilDeck,trees,foliage,pollen };
        }

        private void BuildOpeningArchive()
        {
            openingArchive=Group("Archive fragments from the later galleries",root.transform);
            var steel=Geometry();
            var signals=Geometry();
            var cool = new Color(.35f,.65f,.73f);
            var bronze=new Color(.53f,.43f,.26f);
            for (int side=-1;side<=1;side+=2)
            for (int row=0;row<5;row++)
            {
                float z=18.4f+row*2.32f+side*.45f;
                var foot=new Vector3(side*(2.52f+.30f*Mathf.Sin(row)),-15.43f,z);
                RuinBar(steel, foot, foot+new Vector3(side*.33f,4.18f,.30f),.13f,.17f,bronze);
                Ring(signals,foot+Vector3.up*(2.23f+.42f*(row%3)),.69f,.015f,row%2==0?cool:Warm,
                    Quaternion.Euler(8,22*side,17*row),38,.84f);
                RuinBar(signals,foot+Vector3.up*.29f,foot+Vector3.up*.37f+Vector3.forward*1.88f,.031f,.024f,cool);
            }
            PlaceStarBackdrop(openingArchive,"Archive columns among the broken roots",steel,stone);
            Light(PlaceStarBackdrop(openingArchive,"Gold and cyan gallery conduits",signals,glow),Color.white,.79f);
        }
    }
}
