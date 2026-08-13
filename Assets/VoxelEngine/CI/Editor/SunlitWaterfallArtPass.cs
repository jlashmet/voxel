using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallArtPass
    {
        private static bool _prepared;
        private static Transform _root;
        private static readonly List<Object> Created = new List<Object>();

        private static Material Grass, Grass2, Cliff, Cliff2, Stone, Stone2, Moss, Moss2;
        private static Material Bark, Leaf, Leaf2, Water, Fall, Foam, CloudMat, Sky, Roof;
        private static Material WhiteFlower, PinkFlower, BlueFlower, YellowFlower;

        public static void Apply(Camera camera)
        {
            if (_prepared || camera == null) return;
            _prepared = true;

            GameObject prototype = GameObject.Find("Sunlit Waterfall Art Kit");
            Vector3 origin = ResolveOrigin(prototype);
            if (prototype != null) prototype.SetActive(false);

            GameObject rootObject = new GameObject("Sunlit Waterfall Target Scene");
            _root = rootObject.transform;
            Created.Add(rootObject);

            BuildPalette();
            Configure(camera, origin);
            BuildSky(origin);
            BuildTerrain(origin);
            BuildRuins(origin);
            BuildWater(origin);
            BuildVegetation(origin);
            BuildCastle(origin);
        }

        private static Vector3 ResolveOrigin(GameObject prototype)
        {
            if (prototype != null)
            {
                Transform[] all = prototype.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "Foreground turf")
                        return all[i].position - new Vector3(-0.15f, 0.10f, -3.70f);
                }
            }
            return Vector3.zero;
        }

        private static void BuildPalette()
        {
            Grass = Smooth("Sunlit grass", new Color(0.43f,0.61f,0.18f));
            Grass2 = Smooth("Sunlit grass light", new Color(0.54f,0.70f,0.23f));
            Cliff = Smooth("Garden cliff", new Color(0.38f,0.39f,0.32f));
            Cliff2 = Smooth("Warm garden cliff", new Color(0.49f,0.48f,0.39f));
            Stone = Smooth("Warm ruin stone", new Color(0.69f,0.64f,0.53f),0.04f);
            Stone2 = Smooth("Sunlit ruin stone", new Color(0.81f,0.75f,0.62f),0.04f);
            Moss = Smooth("Moss", new Color(0.28f,0.47f,0.12f));
            Moss2 = Smooth("Sunlit moss", new Color(0.43f,0.60f,0.17f));
            Bark = Smooth("Oak bark", new Color(0.29f,0.18f,0.085f));
            Leaf = Smooth("Oak leaves", new Color(0.25f,0.46f,0.12f));
            Leaf2 = Smooth("Oak leaves sun", new Color(0.46f,0.63f,0.19f));
            Water = Transparent("Turquoise water", new Color(0.08f,0.65f,0.83f,0.88f), new Color(0.02f,0.12f,0.18f));
            Fall = Transparent("Waterfall", new Color(0.67f,0.91f,0.98f,0.84f), new Color(0.12f,0.21f,0.25f));
            Foam = Transparent("Water foam", new Color(0.97f,0.995f,1f,0.72f), new Color(0.25f,0.29f,0.31f));
            CloudMat = Smooth("Cloud", new Color(0.99f,0.99f,0.97f),0.01f,new Color(0.08f,0.08f,0.07f));
            Sky = Smooth("Blue sky", new Color(0.06f,0.47f,0.88f),0f,new Color(0.10f,0.25f,0.48f));
            Roof = Smooth("Castle roof", new Color(0.46f,0.43f,0.40f),0.05f);
            WhiteFlower = Smooth("White flower", new Color(0.99f,0.98f,0.90f));
            PinkFlower = Smooth("Pink flower", new Color(0.95f,0.50f,0.59f));
            BlueFlower = Smooth("Blue flower", new Color(0.36f,0.67f,0.92f));
            YellowFlower = Smooth("Yellow flower", new Color(0.98f,0.73f,0.15f));
        }

        private static void Configure(Camera camera, Vector3 o)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f,0.47f,0.87f,1f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 110f;
            camera.transform.position = o + new Vector3(0.55f,4.35f,-21.8f);
            camera.transform.LookAt(o + new Vector3(-0.1f,3.20f,3.8f));

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f,0.78f,0.94f);
            RenderSettings.ambientEquatorColor = new Color(0.58f,0.60f,0.49f);
            RenderSettings.ambientGroundColor = new Color(0.25f,0.27f,0.18f);
            RenderSettings.ambientIntensity = 0.78f;

            Light light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.color = new Color(1f,0.93f,0.76f);
                light.intensity = 1.18f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.40f;
                light.transform.rotation = Quaternion.Euler(38f,-32f,0f);
            }
        }

        private static void BuildSky(Vector3 o)
        {
            GameObject sky = Primitive(PrimitiveType.Quad,"Physical blue sky",Sky);
            sky.transform.position = o + new Vector3(0f,9.5f,50f);
            sky.transform.localScale = new Vector3(44f,29f,1f);
            Renderer sr = sky.GetComponent<Renderer>();
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;

            Cloud(o + new Vector3(-7.5f,10.8f,31f),1.05f);
            Cloud(o + new Vector3(0.0f,12.6f,35f),1.45f);
            Cloud(o + new Vector3(9.4f,11.2f,33f),1.18f);
            Cloud(o + new Vector3(4.5f,8.8f,29f),0.70f);
        }

        private static void BuildTerrain(Vector3 o)
        {
            Terrace("Hero terrace",o + new Vector3(0f,0.18f,-3.7f),11.8f,7.2f,2.35f,2.1f,Grass,Cliff,true);
            Terrace("Left ruin garden",o + new Vector3(-7.1f,2.45f,1.0f),7.7f,7.8f,3.5f,1.75f,Grass2,Cliff2,false);
            Terrace("Middle garden",o + new Vector3(-1.7f,2.90f,5.3f),10.6f,6.8f,2.75f,1.9f,Grass2,Cliff,false);

            Terrace("Cascade zero",o + new Vector3(5.1f,0.55f,-0.2f),7.4f,5.2f,2.1f,1.4f,Grass,Cliff,false);
            Terrace("Cascade one",o + new Vector3(6.2f,2.55f,3.0f),7.1f,5.0f,2.2f,1.35f,Grass2,Cliff2,false);
            Terrace("Cascade two",o + new Vector3(7.1f,4.55f,6.3f),6.7f,4.8f,2.2f,1.3f,Grass,Cliff,false);
            Terrace("Cascade three",o + new Vector3(7.8f,6.50f,9.5f),6.3f,4.6f,2.25f,1.25f,Grass2,Cliff2,false);

            Terrace("Front left island",o + new Vector3(-5.8f,-0.50f,-7.4f),4.2f,3.0f,1.30f,0.95f,Grass,Cliff,false);
            Terrace("Front right island",o + new Vector3(5.8f,-0.66f,-6.7f),4.7f,3.3f,1.35f,1.0f,Grass2,Cliff,false);

            for (int i=0;i<8;i++)
            {
                float x=-4.1f+i*1.18f;
                StoneBlock(o + new Vector3(x,0.54f+(i%2)*0.05f,-7.12f+(i%3)*0.08f),
                           new Vector3(0.95f,0.58f,0.78f),i%3==0?Stone2:Stone,700+i);
            }
        }

        private static void BuildRuins(Vector3 o)
        {
            Vector3 c=o + new Vector3(-6.0f,4.55f,-0.15f);
            float radius=2.65f;
            for (int side=-1;side<=1;side+=2)
            {
                for (int row=0;row<7;row++)
                {
                    StoneBlock(c + new Vector3(side*radius,-3.95f+row*0.72f,0f),
                               new Vector3(0.88f,0.68f,0.82f),(row+side)%3==0?Stone2:Stone,100+row+side*9);
                }
            }
            for (int i=0;i<=13;i++)
            {
                if (i==9) continue;
                float a=Mathf.Lerp(180f,0f,i/13f)*Mathf.Deg2Rad;
                GameObject s=StoneBlock(c + new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0f),
                                        new Vector3(0.90f,0.65f,0.84f),i%4==0?Stone2:Stone,180+i);
                s.transform.rotation=Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }

            Vector3 lc=o + new Vector3(-6.5f,0.95f,-3.2f);
            float lr=1.45f;
            for (int side=-1;side<=1;side+=2)
                for (int row=0;row<4;row++)
                    StoneBlock(lc + new Vector3(side*lr,-1.70f+row*0.56f,0f),new Vector3(0.63f,0.50f,0.67f),Stone,240+row+side);
            for (int i=0;i<=9;i++)
            {
                float a=Mathf.Lerp(180f,0f,i/9f)*Mathf.Deg2Rad;
                GameObject s=StoneBlock(lc + new Vector3(Mathf.Cos(a)*lr,Mathf.Sin(a)*lr,0f),new Vector3(0.64f,0.48f,0.67f),i%3==0?Stone2:Stone,270+i);
                s.transform.rotation=Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }

            for (int i=0;i<6;i++)
                StoneBlock(o + new Vector3(7.0f+(i%2)*0.78f,0.05f+i*0.50f,-4.8f+(i%2)*0.18f),new Vector3(0.82f,0.66f,0.78f),i%2==0?Stone:Stone2,310+i);

            AddArchGrowth(c);
        }

        private static void BuildWater(Vector3 o)
        {
            Pool("Front channel",o + new Vector3(1.6f,-1.04f,-6.3f),13.3f,5.3f,1.9f);
            Pool("Lower pool",o + new Vector3(5.0f,-0.05f,-1.35f),6.5f,3.2f,1.2f);
            Pool("Middle pool",o + new Vector3(6.2f,1.98f,2.15f),5.5f,2.8f,1.0f);
            Pool("Upper pool",o + new Vector3(7.2f,3.98f,5.55f),4.8f,2.5f,0.9f);
            Pool("Top pool",o + new Vector3(7.9f,5.96f,8.75f),4.0f,2.2f,0.8f);

            Waterfall(o + new Vector3(5.55f,2.05f,1.05f),o + new Vector3(5.0f,0.05f,-0.15f),2.15f);
            Waterfall(o + new Vector3(6.65f,4.04f,4.45f),o + new Vector3(6.15f,2.04f,3.2f),1.92f);
            Waterfall(o + new Vector3(7.55f,6.02f,7.65f),o + new Vector3(7.08f,4.03f,6.45f),1.70f);
            Waterfall(o + new Vector3(8.0f,0.55f,-3.8f),o + new Vector3(7.7f,-1.20f,-5.1f),1.0f);
        }

        private static void BuildVegetation(Vector3 o)
        {
            Vector3 b=o + new Vector3(-9.3f,1.0f,0.1f);
            Capsule("Oak trunk",b,b + new Vector3(0.25f,7.2f,0.4f),0.70f,Bark);
            Capsule("Oak left bough",b + new Vector3(0f,4.4f,0f),b + new Vector3(-3.1f,7.7f,0.5f),0.38f,Bark);
            Capsule("Oak right bough",b + new Vector3(0.1f,4.9f,0.2f),b + new Vector3(3.2f,7.5f,1.2f),0.35f,Bark);
            Capsule("Oak crown bough",b + new Vector3(0.2f,5f,0.2f),b + new Vector3(-0.4f,8.6f,0.8f),0.31f,Bark);

            Vector3[] crown={
                new Vector3(-3.4f,7.6f,0.2f),new Vector3(-2.1f,8.6f,0.4f),new Vector3(-0.4f,8.9f,0.8f),
                new Vector3(1.5f,8.5f,1.0f),new Vector3(3.0f,7.7f,1.1f),new Vector3(-4.0f,6.7f,0.6f),
                new Vector3(-1.2f,7.2f,-0.3f),new Vector3(1.3f,7.1f,-0.2f),new Vector3(3.8f,6.7f,0.6f)};
            for (int i=0;i<crown.Length;i++)
            {
                GameObject leaf=Primitive(PrimitiveType.Sphere,"Rounded oak canopy",i%3==0?Leaf2:Leaf);
                leaf.transform.position=b+crown[i];
                leaf.transform.localScale=new Vector3(2.8f+(i%2)*0.45f,2.1f+(i%3)*0.18f,2.3f+(i%2)*0.35f);
            }

            Shrub(o + new Vector3(-4.9f,0.95f,-5.3f),1.1f,Moss);
            Shrub(o + new Vector3(-7.8f,3.25f,-0.4f),1.0f,Moss2);
            Shrub(o + new Vector3(3.9f,0.78f,-4.4f),0.9f,Moss);
            Shrub(o + new Vector3(6.0f,2.85f,2.4f),0.72f,Moss2);
            Shrub(o + new Vector3(7.5f,4.85f,5.6f),0.68f,Moss);

            FlowerPatch(o + new Vector3(-3.5f,1.40f,-4.9f),WhiteFlower,1);
            FlowerPatch(o + new Vector3(-1.9f,1.42f,-5.4f),PinkFlower,2);
            FlowerPatch(o + new Vector3(2.7f,1.34f,-4.1f),BlueFlower,3);
            FlowerPatch(o + new Vector3(-7.5f,4.25f,-0.6f),YellowFlower,4);
            FlowerPatch(o + new Vector3(-6.5f,4.05f,1.0f),WhiteFlower,5);
            FlowerPatch(o + new Vector3(5.0f,1.62f,-0.1f),PinkFlower,6);
        }

        private static void BuildCastle(Vector3 o)
        {
            Vector3 hill=o + new Vector3(8.8f,7.8f,22.5f);
            Terrace("Distant castle hill",hill,8.3f,6.4f,5.6f,1.8f,Grass2,Cliff2,false);
            Vector3 b=hill + new Vector3(0f,0.55f,0f);
            CastleTower(b,0.78f,5.7f);
            CastleTower(b + new Vector3(-1.8f,-0.2f,-0.15f),0.58f,4.2f);
            CastleTower(b + new Vector3(1.8f,-0.1f,0.3f),0.56f,4.0f);
            CastleTower(b + new Vector3(0.8f,1.4f,0.2f),0.43f,3.5f);
            GameObject keep=Primitive(PrimitiveType.Cube,"Distant castle keep",Stone2);
            keep.transform.position=b + new Vector3(0f,1.55f,0.5f);
            keep.transform.localScale=new Vector3(3.8f,3.0f,2.2f);
            Waterfall(hill + new Vector3(-2.8f,-0.1f,-1.3f),hill + new Vector3(-3.0f,-4.7f,-1.6f),1.05f);
        }

        private static void Terrace(string name,Vector3 top,float width,float depth,float height,float radius,Material turf,Material cliff,bool path)
        {
            Mesh side=TerraceMesh(width,depth,height,radius,6,false);
            Mesh cap=TerraceMesh(width*0.97f,depth*0.97f,0.04f,radius*0.94f,6,true);
            Created.Add(side);Created.Add(cap);
            GameObject s=MeshObject(name+" cliff",side,cliff);s.transform.position=top;
            GameObject t=MeshObject(name+" turf",cap,turf);t.transform.position=top+Vector3.up*0.035f;

            if (width>7f)
            {
                for (int i=0;i<5;i++)
                {
                    float f=(i-2)/2f;
                    Shrub(top + new Vector3(f*width*0.36f,0.14f,-depth*0.46f),0.40f+(i%2)*0.08f,Moss);
                }
            }
            if (path)
            {
                for (int i=0;i<7;i++)
                    StoneBlock(top + new Vector3(-1.9f+i*0.65f,0.14f,-0.55f+(i%2)*0.05f),new Vector3(0.52f,0.12f,0.45f),i%3==0?Stone2:Stone,800+i);
            }
        }

        private static void AddArchGrowth(Vector3 c)
        {
            for (int i=0;i<7;i++)
            {
                GameObject m=Primitive(PrimitiveType.Sphere,"Arch moss cushion",i%2==0?Moss2:Moss);
                m.transform.position=c + new Vector3(-2.45f+i*0.72f,2.72f+(i%3)*0.16f,-0.35f);
                m.transform.localScale=new Vector3(0.82f,0.22f,0.46f);
            }
            for (int strand=0;strand<4;strand++)
            {
                Vector3 start=c + new Vector3(-2.1f+strand*1.25f,2.65f-strand*0.08f,-0.42f);
                for (int i=0;i<5+strand;i++)
                {
                    GameObject leaf=Primitive(PrimitiveType.Sphere,"Hanging ivy",Leaf);
                    leaf.transform.position=start + new Vector3(Mathf.Sin(i*1.5f)*0.12f,-i*0.35f,0f);
                    leaf.transform.localScale=new Vector3(0.26f,0.18f,0.10f);
                }
            }
        }

        private static void Pool(string name,Vector3 centre,float width,float depth,float radius)
        {
            Mesh m=TerraceMesh(width,depth,0.02f,radius,8,true);Created.Add(m);
            GameObject go=MeshObject(name,m,Water);go.transform.position=centre;
        }

        private static void Waterfall(Vector3 top,Vector3 bottom,float width)
        {
            Mesh m=Ribbon(top,bottom,width,18);Created.Add(m);MeshObject("Waterfall sheet",m,Fall);
            Mesh hi=Ribbon(top+new Vector3(0f,0.02f,-0.01f),bottom+new Vector3(0f,0.03f,-0.02f),width*0.36f,18);Created.Add(hi);MeshObject("Waterfall highlight",hi,Foam);
            for (int i=0;i<7;i++)
            {
                float f=(i-3)/3f;
                GameObject puff=Primitive(PrimitiveType.Sphere,"Foam puff",Foam);
                puff.transform.position=bottom + new Vector3(f*width*0.45f,0.02f+(i%2)*0.07f,((i*5)%3-1)*0.08f);
                puff.transform.localScale=new Vector3(width*0.22f,0.13f,0.28f);
            }
        }

        private static GameObject StoneBlock(Vector3 p,Vector3 scale,Material mat,int seed)
        {
            GameObject s=Primitive(PrimitiveType.Capsule,"Rounded ashlar",mat);
            s.transform.position=p;
            s.transform.localScale=new Vector3(scale.x,scale.y*0.52f,scale.z);
            s.transform.rotation=Quaternion.Euler((Hash01(seed+5)-0.5f)*3f,(Hash01(seed+11)-0.5f)*6f,(Hash01(seed+19)-0.5f)*3f);
            return s;
        }

        private static void Shrub(Vector3 p,float scale,Material mat)
        {
            for (int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*0.63f;
                GameObject q=Primitive(PrimitiveType.Sphere,"Rounded shrub",mat);
                q.transform.position=p + new Vector3(Mathf.Cos(a)*scale*0.32f,(i&1)*scale*0.08f,Mathf.Sin(a)*scale*0.24f);
                q.transform.localScale=new Vector3(scale*0.72f,scale*0.42f,scale*0.58f);
            }
        }

        private static void FlowerPatch(Vector3 p,Material petals,int seed)
        {
            for (int i=0;i<5;i++)
            {
                Vector3 q=p + new Vector3((Hash01(seed*19+i)-0.5f)*0.72f,0f,(Hash01(seed*29+i+7)-0.5f)*0.55f);
                float h=0.18f+(i%3)*0.035f;
                Capsule("Flower stem",q,q+Vector3.up*h,0.012f,Leaf);
                GameObject middle=Primitive(PrimitiveType.Sphere,"Flower centre",YellowFlower);
                middle.transform.position=q+Vector3.up*h;middle.transform.localScale=Vector3.one*0.040f;
                for (int j=0;j<5;j++)
                {
                    float a=j*Mathf.PI*2f/5f;
                    GameObject petal=Primitive(PrimitiveType.Sphere,"Flower petal",petals);
                    petal.transform.position=q+Vector3.up*h+new Vector3(Mathf.Cos(a)*0.06f,0f,Mathf.Sin(a)*0.06f);
                    petal.transform.localScale=new Vector3(0.070f,0.022f,0.045f);
                }
            }
        }

        private static void Cloud(Vector3 p,float scale)
        {
            Vector3[] offsets={new Vector3(-1.15f,-0.05f,0f),new Vector3(-0.45f,0.35f,0.05f),new Vector3(0.25f,0.45f,0.08f),new Vector3(1.0f,0f,0f),new Vector3(0.1f,-0.28f,-0.03f)};
            Vector3[] sizes={new Vector3(1.25f,0.85f,0.85f),new Vector3(1.55f,1.20f,1.0f),new Vector3(1.65f,1.25f,1.05f),new Vector3(1.25f,0.85f,0.85f),new Vector3(1.55f,0.72f,0.95f)};
            for (int i=0;i<offsets.Length;i++)
            {
                GameObject q=Primitive(PrimitiveType.Sphere,"Puffy cloud",CloudMat);
                q.transform.position=p+offsets[i]*scale;q.transform.localScale=sizes[i]*scale;
                Renderer r=q.GetComponent<Renderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private static void CastleTower(Vector3 p,float radius,float height)
        {
            GameObject tower=Primitive(PrimitiveType.Cylinder,"Distant tower",Stone2);
            tower.transform.position=p+Vector3.up*height*0.5f;tower.transform.localScale=new Vector3(radius,height*0.5f,radius);
            Mesh cone=Cone(radius*1.25f,height*0.42f,18);Created.Add(cone);GameObject roof=MeshObject("Castle spire",cone,Roof);roof.transform.position=p+Vector3.up*(height+height*0.20f);
        }

        private static Mesh TerraceMesh(float width,float depth,float height,float radius,int segments,bool topOnly)
        {
            List<Vector2> outline=Outline(width,depth,radius,segments);
            int count=outline.Count;
            if (topOnly)
            {
                Vector3[] v=new Vector3[count+1];int[] tri=new int[count*3];v[0]=Vector3.zero;
                for (int i=0;i<count;i++){v[i+1]=new Vector3(outline[i].x,0f,outline[i].y);int next=(i+1)%count;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=next+1;}
                Mesh m=new Mesh();m.name="Rounded top";m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
            }
            Vector3[] verts=new Vector3[count*2];int[] tris=new int[count*6];
            for (int i=0;i<count;i++)
            {
                Vector2 top=outline[i];Vector2 bottom=top*1.055f;verts[i]=new Vector3(top.x,0f,top.y);verts[i+count]=new Vector3(bottom.x,-height,bottom.y);
                int next=(i+1)%count;int k=i*6;tris[k]=i;tris[k+1]=next;tris[k+2]=i+count;tris[k+3]=next;tris[k+4]=next+count;tris[k+5]=i+count;
            }
            Mesh mesh=new Mesh();mesh.name="Rounded cliff";mesh.vertices=verts;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static List<Vector2> Outline(float width,float depth,float radius,int segments)
        {
            float hx=width*0.5f;float hz=depth*0.5f;float rr=Mathf.Min(radius,Mathf.Min(hx,hz)-0.01f);
            List<Vector2> p=new List<Vector2>((segments+1)*4);
            Corner(p,new Vector2(hx-rr,hz-rr),rr,0f,90f,segments);
            Corner(p,new Vector2(-hx+rr,hz-rr),rr,90f,180f,segments);
            Corner(p,new Vector2(-hx+rr,-hz+rr),rr,180f,270f,segments);
            Corner(p,new Vector2(hx-rr,-hz+rr),rr,270f,360f,segments);
            return p;
        }

        private static void Corner(List<Vector2> p,Vector2 c,float r,float a0,float a1,int segments)
        {
            for (int i=0;i<=segments;i++){float a=Mathf.Lerp(a0,a1,i/(float)segments)*Mathf.Deg2Rad;p.Add(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r);}
        }

        private static Mesh Ribbon(Vector3 top,Vector3 bottom,float width,int segments)
        {
            Vector3 d=bottom-top;Vector3 side=Vector3.Cross(d.normalized,Vector3.up);if(side.sqrMagnitude<0.001f)side=Vector3.right;side.Normalize();
            Vector3[] v=new Vector3[(segments+1)*2];Vector2[] uv=new Vector2[v.Length];int[] tri=new int[segments*12];
            for (int i=0;i<=segments;i++)
            {
                float t=i/(float)segments;Vector3 c=Vector3.Lerp(top,bottom,t)+side*Mathf.Sin(t*11f)*width*0.025f;float w=width*(0.95f+Mathf.Sin(t*7f)*0.04f);int q=i*2;
                v[q]=c-side*w*0.5f;v[q+1]=c+side*w*0.5f;uv[q]=new Vector2(0f,t*3f);uv[q+1]=new Vector2(1f,t*3f);
                if(i<segments){int k=i*12;tri[k]=q;tri[k+1]=q+2;tri[k+2]=q+1;tri[k+3]=q+1;tri[k+4]=q+2;tri[k+5]=q+3;tri[k+6]=q+1;tri[k+7]=q+2;tri[k+8]=q;tri[k+9]=q+3;tri[k+10]=q+2;tri[k+11]=q+1;}
            }
            Mesh m=new Mesh();m.name="Waterfall ribbon";m.vertices=v;m.uv=uv;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Mesh Cone(float radius,float height,int segments)
        {
            Vector3[] v=new Vector3[segments+1];int[] tri=new int[segments*3];v[0]=Vector3.up*height*0.5f;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;v[i+1]=new Vector3(Mathf.Cos(a)*radius,-height*0.5f,Mathf.Sin(a)*radius);int next=(i+1)%segments;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=next+1;}
            Mesh m=new Mesh();m.name="Castle cone";m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Material Smooth(string name,Color color,float smoothness=0.03f,Color? emission=null)
        {
            Shader shader=Shader.Find("VoxelEngine/SunlitSmooth");
            Material m=new Material(shader);m.name=name;m.SetTexture("_MainTex",Texture2D.whiteTexture);m.SetColor("_BaseColor",color);m.SetColor("_EmissionColor",emission??Color.black);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Cull",2f);m.SetFloat("_ZWrite",1f);Created.Add(m);return m;
        }

        private static Material Transparent(string name,Color color,Color emission)
        {
            Material m=Smooth(name,color,0.05f,emission);m.SetFloat("_Cull",0f);m.SetFloat("_ZWrite",0f);m.renderQueue=(int)RenderQueue.Transparent;return m;
        }

        private static GameObject Primitive(PrimitiveType type,string name,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(_root,false);Collider col=go.GetComponent<Collider>();if(col!=null)Object.DestroyImmediate(col);Renderer r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;Created.Add(go);return go;
        }

        private static GameObject MeshObject(string name,Mesh mesh,Material material)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(_root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;Created.Add(go);return go;
        }

        private static void Capsule(string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            Vector3 d=b-a;if(d.sqrMagnitude<0.0001f)return;GameObject c=Primitive(PrimitiveType.Capsule,name,material);c.transform.position=(a+b)*0.5f;c.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);c.transform.localScale=new Vector3(radius*2f,Mathf.Max(radius,d.magnitude*0.5f),radius*2f);
        }

        private static float Hash01(int n)
        {
            unchecked{uint x=(uint)n;x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return(x&0x00ffffff)/16777215f;}
        }
    }
}
