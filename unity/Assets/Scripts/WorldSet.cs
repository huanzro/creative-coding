/* ARTISTIC RATIONALE
 * Roots, stone and stars are one traversable space. The descent passes beneath
 * the same forest floor the figure was reaching across in the earlier shot.
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echoes
{
    internal sealed partial class WorldSet : IDisposable
    {
        public const int Layer = 30;
        public const float SinkZ = 14, CaveFloor = -16.15f, StarY = 31;
        private readonly GameObject root = new GameObject("Echoes 3D environments");
        private readonly System.Random random = new System.Random(1505);
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Material stone, soil, wood, leaves, glow, sky, mist;
        private readonly Mesh beaconMesh;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Transform[] plantRays = new Transform[2], bridge = new Transform[9];
        private readonly MeshRenderer[] plantLamps = new MeshRenderer[2], gates = new MeshRenderer[3];
        private readonly MeshRenderer[] restLamps = new MeshRenderer[4];
        private readonly Transform[] stars = new Transform[3];
        private readonly MeshRenderer[] starLamps = new MeshRenderer[3];
        private readonly Transform[] links = new Transform[3];
        private static readonly Color Warm = new Color(1,.63f,.22f), Jade = new Color(.26f,.90f,.76f),
            Red = new Color(1,.25f,.16f), Pale = new Color(.70f,.83f,1), IrisBlue=new Color(.61f,.51f,1);
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int Lit = Shader.PropertyToID("_Glow");

        public WorldSet(Shader surfaces, Shader skyShader, Shader mistShader)
        {
            root.hideFlags = HideFlags.HideAndDontSave;
            stone = MaterialFor(surfaces, "Granular stone", 0);
            wood = MaterialFor(surfaces, "Wet bark", 1);
            leaves = MaterialFor(surfaces, "Veined foliage", 2);
            glow = MaterialFor(surfaces, "Light inlays", 3);
            soil = MaterialFor(surfaces, "Leaves and soil", 4);
            Grain(stone,"Stone",.69f,.48f);
            Grain(wood,"Bark",.85f,.56f);
            Grain(soil,"Ground",.92f,.49f);
            sky = new Material(skyShader) { name = "Atmosphere" };
            mist = new Material(mistShader) { name = "Light through mist" };
            var shape = new WorldMesh(meshes);
            shape.Rock(Vector3.zero, Vector3.one, Color.white, .33f, 5, 8, .065f);
            beaconMesh = shape.Finish("Faceted seed");
            BuildForest();
            BuildRuins();
            BuildStars();
            BuildCourt();
            BuildFlies();
            shape = new WorldMesh(meshes);
            shape.Box(Vector3.zero, Vector3.one * 2, Quaternion.identity, Color.white);
            Put("Sky volume", shape.Finish("Sky volume"), sky, new Vector3(0,15,34), Vector3.one * 218);
        }

        private static Material MaterialFor(Shader shader, string name, float mode)
        {
            var result = new Material(shader) { name = name };
            result.SetFloat("_Surface", mode);
            return result;
        }

        private static void Grain(Material material,string name,float strength,float scale)
        {
            Texture2D texture=Resources.Load<Texture2D>("WorldMaterials/"+name);
            if(texture==null) return;
            material.SetTexture("_GrainTex",texture);
            material.SetVector("_Grain",new Vector4(strength,scale,0,0));
        }

        private float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();
        private WorldMesh Geometry() => new WorldMesh(meshes);
        internal static float Ground(float x, float z)
        {
            float bank = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.8f, 16, Mathf.Abs(x)));
            return -.37f + Mathf.PerlinNoise(x*.13f+4, z*.135f+4)*.58f +
                bank * (Mathf.Sin(z*.143f+1.3f) * 1.16f + Mathf.PerlinNoise(x*.121f+12,z*.097f+8)*2.1f);
        }

        private void Put(string name, WorldMesh geometry, Material material) =>
            Put(name, geometry.Finish(name), material, Vector3.zero, Vector3.one);

        private Transform Put(string name, Mesh mesh, Material material, Vector3 at, Vector3 scale)
        {
            var node = new GameObject(name) { layer = Layer, hideFlags = HideFlags.HideAndDontSave };
            Transform transform = node.transform;
            transform.SetParent(root.transform, false);
            transform.localPosition = at;
            transform.localScale = scale;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            bool solid = material == stone || material == soil || material == wood || material == leaves;
            renderer.shadowCastingMode = solid ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            renderer.receiveShadows = solid;
            return transform;
        }

        private MeshRenderer Lamp(string name, Vector3 at, float size, Color color, float light)
        {
            var renderer = Put(name, beaconMesh, glow, at, Vector3.one*size).GetComponent<MeshRenderer>();
            Light(renderer, color, light);
            return renderer;
        }

        private void Light(MeshRenderer renderer, Color color, float power)
        {
            properties.SetColor(Tint, color);
            properties.SetFloat(Lit, power);
            renderer.SetPropertyBlock(properties);
        }

        private void BuildCourt()
        {
            var tiles = Geometry();
            var brass = Geometry();
            float start = -34;
            for (int i = 0; i < 8; i++)
            {
                float z = start + i * 2.15f;
                tiles.Box(new Vector3(0, .04f + i*.024f, z), new Vector3(4.6f, .33f, 2.08f),
                    Quaternion.Euler(0, Mathf.Sin(i)*2,0), new Color(.28f,.40f,.37f));
                if (i % 2 != 0) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    tiles.Box(new Vector3(side*2.55f,.64f,z),new Vector3(.43f,1.42f,.44f),Quaternion.identity,new Color(.24f,.34f,.30f));
                    brass.Rock(new Vector3(side*2.55f,1.38f,z),new Vector3(.13f,.18f,.13f),new Color(.95f,.61f,.22f),i*.19f);
                }
            }
            for (int i = 0; i < 3; i++)
                Arch(tiles, 0, .1f, start + 6 + i*4.4f, 2.65f, 2.50f, .47f, .72f,
                    new Color(.29f,.38f,.35f), 14);
            Put("Court stairs and gateway",tiles,stone);
            Put("Court lanterns",brass,glow);
            for(int i=0;i<courtSeals.Length;i++)
                courtSeals[i]=Lamp("Practice response "+(i+1), new Vector3(i==0?-1.1f:i==1?1.45f:.05f,1.23f+i*.15f,start+3+i*2.66f),
                    .125f,Warm,.1f);
        }

        private void BuildForest()
        {
            var ground = Geometry();
            var trunks = Geometry();
            var crowns = Geometry();
            var cover = Geometry();
            var banks = Geometry();
            var seed = Geometry();
            ground.Surface(86, 200, (x,z) =>
            {
                float px = (x-43)*.86f, pz = -42 + z*.9f;
                return new Vector3(px,Ground(px,pz),pz);
            }, p =>
            {
                float trail = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,3.7f,Mathf.Abs(p.x-1.55f*Mathf.Sin(p.z*.12f))));
                Color clay = new Color(.27f,.232f,.164f);
                Color moss = new Color(.177f,.246f,.168f);
                return WorldMesh.Shade(Color.Lerp(clay,moss,trail),.66f+.55f*Mathf.PerlinNoise(p.x*.64f+9,p.z*.54f+9));
            }, p => new Vector2(p.x,p.z-SinkZ).sqrMagnitude < 13.7f);

            for (int lane=0; lane<18; lane++)
            for (int side=-1; side<=1; side+=2)
            for (int row=0; row<3; row++)
            {
                float x = side * (4.7f + row*6.8f + Range(-.9f,2.3f));
                float z = -38 + lane*10 + Range(-4.2f,3.4f);
                Tree(trunks,crowns,new Vector3(x,Ground(x,z)-.28f,z),Range(8,17)+row*1.8f,Range(.37f,1.36f));
            }
            Tree(trunks,crowns,new Vector3(-4.1f,-.24f,6.9f),14.2f,1.33f);
            Tree(trunks,crowns,new Vector3(4.6f,.23f,10.5f),12.7f,1.04f);
            Tree(trunks,crowns,new Vector3(-6.3f,-.22f,20.9f),16.4f,1.48f);

            for (int i=0; i<480; i++)
            {
                float z=Range(-36,135), x=Range(-24,24);
                if (Mathf.Abs(x-1.4f*Mathf.Sin(z*.12f))<Range(1.45f,3.25f) || (new Vector2(x,z-SinkZ)).sqrMagnitude<15) continue;
                float y=Ground(x,z);
                if (i%3==0) banks.Rock(new Vector3(x,y-.07f,z), new Vector3(Range(.27f,1.74f),Range(.22f,.82f),Range(.34f,1.8f)),
                    Color.Lerp(new Color(.21f,.26f,.23f),new Color(.30f,.395f,.24f),Range(0,1)), Range(0,4));
                Fern(cover,new Vector3(x,y,z),Range(.31f,1.14f),Range(0,6.28f));
                if (i%13==0)
                {
                    banks.Tube(new[]{new Vector3(x,y,z),new Vector3(x+.025f,y+.22f,z)},new[]{.038f,.025f},6,new Color(.46f,.39f,.25f));
                    seed.Rock(new Vector3(x+.035f,y+.22f,z),new Vector3(.115f,.044f,.10f), new Color(.65f,.51f,.27f),Range(0,1),4,7);
                }
            }

            for (int side=-1; side<=1; side+=2)
            {
                for (int row=0; row<49; row++)
                {
                    float z=-11+row*2.93f;
                    float x = side*Range(2.3f,4.2f)+Mathf.Sin(z*.1f)*1.2f;
                    if (new Vector2(x,z-SinkZ).sqrMagnitude < 18) continue;
                    Fern(cover,new Vector3(x,Ground(x,z),z),Range(.65f,1.44f),Range(0,6));
                }
                for (int i=0;i<14;i++)
                {
                    float a=i*Mathf.PI*2/14;
                    var p=new Vector3(Mathf.Sin(a)*3.8f,-.2f+Range(-.16f,.13f),SinkZ+Mathf.Cos(a)*3.7f);
                    banks.Rock(p,new Vector3(Range(.63f,1.12f),Range(.19f,.64f),Range(.65f,1.28f)),
                        new Color(.27f,.31f,.23f),i*.55f,6,10);
                }
            }
            Put("Forest terrain above the shaft",ground,soil);
            BuildWindRoute();
            Put("Trees, roots and branches",trunks,wood);
            Put("Forest canopy",crowns,leaves);
            Put("Ferns, leaf litter and grass",cover,leaves);
            Put("Wet glacial rocks",banks,stone);
            Put("Light under the mushrooms",seed,glow);
            LightVolume(new Vector3(5.1f,13.6f,12.3f),new Vector3(.72f,.3f,7.1f),
                .16f,1.84f,new Color(.90f,.71f,.42f,.43f),"Sun between the branches");
            LightVolume(new Vector3(4.7f,15.2f,30.1f),new Vector3(-1.1f,.1f,22.25f),
                .26f,2.89f,new Color(.78f,.65f,.41f,.27f),"Sun across the forest walk");
            for(int i=0;i<3;i++)
            {
                float side=i%2==0?-1:1, z=48+i*27.4f;
                LightVolume(new Vector3(side*7.1f,15.8f,z+2.7f),new Vector3(side*1.24f,.18f,z),
                    .19f,1.45f,new Color(.84f,.77f,.52f,.31f),"Canopy light at a clearing");
            }

            for (int i = 0; i < plantRays.Length; i++)
            {
                float at=27.2f+i*5.45f;
                int side=i==0?-1:1;
                Vector3 knot=new Vector3(side*2.55f,.44f,at);
                plantLamps[i]=Lamp("Root seed "+(i+1),knot,.16f,Warm,.1f);
                var veins=Geometry();
                for (int j=0; j<4; j++)
                {
                    Vector3 end=knot+new Vector3(-side*(1.2f+j*.52f),-.34f,2.1f+j*.44f);
                    veins.Tube(new[]{knot,Vector3.Lerp(knot,end,.47f)+new Vector3(.13f,-.16f,.20f),end},
                        new[]{.026f,.017f,.009f},6,Warm);
                }
                Mesh mesh=veins.Finish("Glow along the roots");
                plantRays[i]=Put("Root response",mesh,glow,Vector3.zero,Vector3.one);
                for (int j=0; j<2; j++)
                {
                    Vector3 rest=knot+new Vector3(-side*(1.65f+j*.50f),-.32f,3.8f+j*4.35f);
                    banks = Geometry();
                    banks.Rock(rest,new Vector3(.95f,.21f,.72f),new Color(.32f,.43f,.33f),i+j*.31f);
                    Put("Moss rest stone",banks,stone);
                    restLamps[i*2+j]=Lamp("Rest stone guide",rest+Vector3.up*.28f,.086f,Warm,.38f);
                }
            }
        }

        private void BuildWindRoute()
        {
            var structure=Geometry();
            var guides=Geometry();
            for(int side=-1;side<=1;side+=2)
            {
                var path=new Vector3[17];
                var widths=new float[17];
                for(int i=0;i<path.Length;i++)
                {
                    float u=i/16f;
                    path[i]=new Vector3(4.3f+side*.63f+Mathf.Sin(u*2.73f)*1.12f,
                        1.55f+Mathf.Sin(u*2.54f)*3.2f,23.6f+u*18f);
                    widths[i]=.16f+.05f*Mathf.Sin(i*.91f);
                    if(side<0)
                        structure.Box(path[i]+new Vector3(.6f,0,0),new Vector3(1.65f,.21f,.98f),
                            Quaternion.Euler(0,i*1.16f,Mathf.Sin(i)*7),new Color(.31f,.24f,.153f));
                }
                structure.Tube(path,widths,8,new Color(.315f,.23f,.149f));
                for(int i=2;i<path.Length;i+=3)
                    guides.Rock(path[i]+Vector3.up*.12f,new Vector3(.041f,.08f,.044f),
                        new Color(.72f,.64f,.34f),i*.41f,3,6);
            }
            Put("Raised wind route",structure,wood);
            Put("Wind route lamps",guides,glow);
        }

        private void Tree(WorldMesh bark, WorldMesh crowns, Vector3 at, float height, float width)
        {
            Vector3 wind = new Vector3(Range(-2.1f,1.9f),0,Range(-1.5f,1.9f));
            Color color=Color.Lerp(new Color(.225f,.171f,.127f),new Color(.345f,.258f,.174f),Range(0,1));
            var baseTrunk=at+Vector3.up*(height*.10f);
            var fork=at+Vector3.up*(height*.64f)+wind*.7f;
            var top=at+Vector3.up*height+wind;
            bark.Tube(new[]{at,baseTrunk,fork-Vector3.up*height*.19f,fork,top},
                new[]{width*1.46f,width*.91f,width*.68f,width*.43f,width*.083f},13,color);
            int roots = width > .75f ? 7 : 4;
            for (int i=0;i<roots;i++)
            {
                float a=i*6.28f/roots+Range(-.25f,.25f);
                var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var toe=at+direction*Range(width*2.2f,width*3.46f);
                toe.y=Ground(toe.x,toe.z)-.08f;
                bark.Tube(new[]{baseTrunk,at+direction*width*1.27f+Vector3.up*width*.18f,toe},
                    new[]{width*.34f,width*.28f,width*.017f},8,color);
            }
            int branches=width>1 ? 8 : 5;
            for(int i=0;i<branches;i++)
            {
                float a=i*2.396f+Range(0,.64f);
                Vector3 horizontal=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float steer=height*Range(.12f,.29f);
                Vector3 shoot=fork+horizontal*steer*.51f+Vector3.up*Range(.4f,1.8f);
                Vector3 end=fork+horizontal*steer+Vector3.up*height*Range(.13f,.31f);
                bark.Tube(new[]{fork-Vector3.up*Range(.1f,1.5f),shoot,end},new[]{width*.29f,width*.144f,width*.025f},8,color);
                Vector3 twig=Vector3.Lerp(shoot,end,.59f)+new Vector3(-horizontal.z, .63f, horizontal.x)*steer*.23f;
                bark.Tube(new[]{shoot,Vector3.Lerp(shoot,twig,.55f),twig},new[]{width*.092f,width*.06f,width*.008f},6,color);
                Color leaf=Color.Lerp(new Color(.145f,.295f,.15f),new Color(.39f,.48f,.22f),Range(.06f,.84f));
                for(int j=0;j<4;j++)
                {
                    Vector3 p=end+new Vector3(Range(-1.1f,1.1f),Range(-.76f,1.37f),Range(-1.2f,1.2f));
                    float radius=Range(.75f,1.48f)*Mathf.Clamp(height/12,.6f,1.5f);
                    crowns.Rock(p,new Vector3(radius*1.31f,radius*.57f,radius),leaf,Range(0,5),4,9,.38f);
                    for(int k=0;k<4;k++)
                    {
                        var root=p+new Vector3(Range(-radius,radius),Range(-.5f,.5f),Range(-radius,radius));
                        crowns.Leaf(root,root+new Vector3(Range(-.7f,.7f),Range(-.54f,.21f),Range(-.7f,.7f)),
                            Vector3.right,.19f,WorldMesh.Shade(leaf,Range(.8f,1.29f)));
                    }
                }
            }
            crowns.Rock(top,new Vector3(height*.17f,height*.075f,height*.15f),new Color(.32f,.437f,.197f),height,5,11,.30f);
        }

        private void Fern(WorldMesh foliage,Vector3 foot,float scale,float rotation)
        {
            Color color=Color.Lerp(new Color(.14f,.30f,.143f),new Color(.48f,.53f,.224f),Range(0,1));
            int heads = scale>.74f?6:4;
            for(int i=0;i<heads;i++)
            {
                float angle=rotation+i*(6.283f/heads);
                Vector3 forward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 across=new Vector3(-forward.z,0,forward.x);
                for(int j=1;j<=5;j++)
                {
                    float u=j/6f;
                    Vector3 leaf=foot+forward*scale*u+Vector3.up*(scale*(.06f+Mathf.Sin(u*2.14f)*.72f));
                    float spread=scale*(.25f*Mathf.Sin(u*2.63f)+.05f);
                    for (int side=-1;side<=1;side+=2)
                    {
                        Vector3 tip=leaf+across*(spread*side)+forward*(spread*.61f)+Vector3.up*(scale*.045f);
                        foliage.Leaf(leaf,tip,forward,spread*.164f,WorldMesh.Shade(color,.76f+u*.41f));
                    }
                }
                foliage.Leaf(foot,foot+forward*scale*.28f+Vector3.up*scale*.64f,across,scale*.038f,WorldMesh.Shade(color,.69f));
            }
        }

        private static void Arch(WorldMesh mesh,float center,float ground,float z,float radius,float sill,
            float band,float thickness,Color color,int count)
        {
            for (int i=0;i<count;i++)
            {
                float angle=Mathf.PI*(i+.5f)/count;
                Vector3 at=new Vector3(center+Mathf.Cos(angle)*radius,ground+sill+Mathf.Sin(angle)*radius,z);
                mesh.Box(at,new Vector3(band,Mathf.PI*radius/count*.95f,thickness),
                    Quaternion.Euler(0,0,angle*Mathf.Rad2Deg),WorldMesh.Shade(color,.86f+.145f*Mathf.Sin(i*3.21f)));
            }
            for(int sign=-1;sign<=1;sign+=2)
            for(int i=0;i<4;i++)
                mesh.Box(new Vector3(center+sign*radius,ground+sill*(i+.5f)/4,z),
                    new Vector3(band*(1.26f-i*.05f),sill*.243f,thickness*1.06f),
                    Quaternion.Euler(0,i%2*2,0),WorldMesh.Shade(color,.91f+i*.03f));
        }

        public void Dispose()
        {
            forestPredator?.Dispose();
            DisposeRuinTunnel();
            DisposeMachine();
            DisposeMaze();
            UnityEngine.Object.Destroy(root);
            foreach(var mesh in meshes) UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(stone);
            UnityEngine.Object.Destroy(soil);
            UnityEngine.Object.Destroy(wood);
            UnityEngine.Object.Destroy(leaves);
            UnityEngine.Object.Destroy(glow);
            UnityEngine.Object.Destroy(sky);
            UnityEngine.Object.Destroy(mist);
        }
    }
}
