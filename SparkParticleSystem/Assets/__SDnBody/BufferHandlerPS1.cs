﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class BufferHandlerPS1 : MonoBehaviour
{
	public const int READ = 1;
	public const int WRITE = 0;
	public Material material;
	public Material lightHaloMat;

	ComputeBuffer[] PosBuffer = new ComputeBuffer[2];
	ComputeBuffer[] VelBuffer = new ComputeBuffer[2];
	ComputeBuffer[] IdAgeBuffer = new ComputeBuffer[2];

	public Text actualspawn;

	ComputeBuffer DeadBuffer;
	ComputeBuffer LiveBuffer;


	ComputeBuffer argBuffer;
	ComputeBuffer deadBuffArgBuff;
	public bool debugframe = false;
	public ComputeShader cShade;
	public Transform gravityObject;



	public int count = 65536;//131072//262144//524288//1048576
							 //public float size = 5.0f;
							 //public float zSize = 20;
							 //public float gravity = 1;
	public float downDampening = 0.01f;
	public float upDampening = .02f;
	int sqrtCount;

	public const string _SimKernelName = "CSMain";//"CSTwirl";
	public const string _SpawnKernelName = "CSSpawnCone";


	public float singularityStickDistance = 10f;

	public GameObject particleCountDisplayerTextObj;
	public static BufferHandlerPS1 inst;



	public int frameSpacing = 0;
	int curFrameCounter = 0;

	public float simSpeed = 1f;
	[HideInInspector]
	public float IsPlaying = 1f;
	float lastVelMod;
	bool isPause = true;

	MiscPSystemControls psystem;

	[HideInInspector]
	public int liveParticles = 0;
	public int deadParticles = 0;

	void Start()
	{
		psystem = MiscPSystemControls.inst;
		Application.targetFrameRate = -1;
		BufferHandlerPS1.inst = this;

		ParticleCountItterator.Init(this);


		int sqrtCount = (int)Mathf.Sqrt(count);
		PosBuffer[READ] = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Default);
		VelBuffer[READ] = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Default);
		PosBuffer[WRITE] = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Default);
		VelBuffer[WRITE] = new ComputeBuffer(count, sizeof(float) * 3, ComputeBufferType.Default);

		IdAgeBuffer[WRITE] = new ComputeBuffer(count, sizeof(float) * 2, ComputeBufferType.Default);
		IdAgeBuffer[READ] = new ComputeBuffer(count, sizeof(float) * 2, ComputeBufferType.Default);

		DeadBuffer = new ComputeBuffer(count, sizeof(int), ComputeBufferType.Append);
		LiveBuffer = new ComputeBuffer(count, sizeof(int), ComputeBufferType.Counter);

		Vector3[] points = new Vector3[count];
		Vector3[] velocities = new Vector3[count];
		Vector2[] idAges = new Vector2[count];
		int[] indecies = new int[count];

		argBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
		deadBuffArgBuff = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

		Random.InitState(0);
		for (int i = 0; i < count; i++)
		{
			indecies[i] = i;
			points[i] = new Vector3(-3f + (float)((int)(i / 1000)) * 0.3f, (i % 1000) / 100f, -5);
			velocities[i] = Vector3.one * 0.01f;

			idAges[i] = new Vector2(0, -1.0f);
		}
		PosBuffer[READ].SetData(points);
		PosBuffer[WRITE].SetData(points);

		VelBuffer[READ].SetData(velocities);
		VelBuffer[WRITE].SetData(velocities);

		IdAgeBuffer[READ].SetData(idAges);
		IdAgeBuffer[WRITE].SetData(idAges);

		DeadBuffer.SetData(indecies);
		DeadBuffer.SetCounterValue((uint)(count));
		lastVelMod = IsPlaying;



	}

	void Update()
	{
		if(curFrameCounter >= frameSpacing)
		{
			DoUpdate();
			curFrameCounter = 0;
		}

		curFrameCounter++;
	}


	void DoUpdate()
	{
		if(psystem == null)
			psystem = MiscPSystemControls.inst;


		Vector2 screnpont = Input.mousePosition;
		Vector3 singularity = Camera.main.ScreenToWorldPoint(new Vector3(screnpont.x, screnpont.y, singularityStickDistance));
		if(gravityObject != null)
			singularity = gravityObject.position;


        cShade.SetVector("_SingularityPosANDdt", new Vector4(singularity.x, singularity.y, singularity.z, Time.deltaTime * Mathf.Abs(simSpeed)));
		cShade.SetFloat("_Time",  Time.time);

		Vector4 gravDirAndStr = new Vector4(psystem.GravityDir.x, psystem.GravityDir.y, psystem.GravityDir.z, psystem.GravityStrength);
		cShade.SetVector("_GravityDirAndStr", gravDirAndStr);

		cShade.SetVector("_PartBounceFricDrag", psystem.BounceFricDrag);

		cShade.SetVector("_SuckstrInOutdistInOut", psystem.suckInnerOuter);
		cShade.SetVector("_EmitVelRotXYapertureXY", psystem.emitVelDirData);

        cShade.SetVector("_SuckDampSphereMousedown", new Vector4(psystem.SuckCaptureDampening, downDampening, 000000, IsPlaying));//ISDOWN

		cShade.SetVector("_EmitVelPosVariance", psystem.emitVelspeedrangePosoffrange);
		cShade.SetVector("_EmitPosRotXYapertureXY", psystem.emitPosRotAperature);
		cShade.SetVector("_EmitPos", psystem.emitPosBase);

		cShade.SetVector("_PartAgeVariance", psystem.ageMinVar);

		cShade.SetVector("_WindDats", psystem.windData);
		//resets the livebuffer, basically emptying it out.
		LiveBuffer.SetCounterValue(0);

        Shader.SetGlobalVector("_MaxAge", new Vector4(psystem.ParticleAgeMin, psystem.ParticleAgeVariation, psystem.ParticleAgeVariation + psystem.ParticleAgeMin, 0.0f));

		DoSpawning();

		DoSimming();

		if(debugframe)
		{
			debugframe = false;

/*			ComputeBuffer argBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
			int[] args = new int[]{ 0, 1, 0, 0 };

			argBuffer.SetData(args);

			ComputeBuffer.CopyCount(LiveBuffer, argBuffer, 0);

			argBuffer.GetData(args);

			string toDebug = "";
			toDebug += "\nvertex count " + args[0];
			toDebug += "\ninstance count " + args[1];
			toDebug += "\nstart vertex " + args[2];
			toDebug += "\nstart instance " + args[3];

			Debug.Log("TODEBUG" + toDebug);
*/		}




		Swap(PosBuffer);
		Swap(VelBuffer);
		Swap(IdAgeBuffer);
	

		if(!Input.GetKey(KeyCode.LeftShift))
		{
			this.singularityStickDistance -= Input.mouseScrollDelta.y;
//			Debug.Log(-Input.mouseScrollDelta.y +" singularity: " + this.singularityStickDistance);
		}


		particleCountDisplayerTextObj.GetComponent<Text>().text = "Simulated Particles:\t\t\t" + this.liveParticles  + "\nMax Particles in System:\t" + this.count;

	}

	void Swap(ComputeBuffer[] buffer) 
	{
		ComputeBuffer tmp = buffer[READ];
		buffer[READ] = buffer[WRITE];
		buffer[WRITE] = tmp;
	}


	int[] GetArgs(ComputeBuffer compOBuffToCheck, ComputeBuffer pargsBuffer)
	{
		int[] args = new int[]{ 0, 1, 0, 0 };
		pargsBuffer.SetData(args);
		ComputeBuffer.CopyCount(compOBuffToCheck, pargsBuffer, 0);
		pargsBuffer.GetData(args);

		return args;
	}



    float spawnCount = 0;

	void DoSpawning()
	{
		//Sets the buffers of the spawn function 
		cShade.SetBuffer(cShade.FindKernel(_SpawnKernelName), "CdeadList", DeadBuffer);
		cShade.SetBuffer(cShade.FindKernel(_SpawnKernelName), "WvertPos", PosBuffer[READ]);
		cShade.SetBuffer(cShade.FindKernel(_SpawnKernelName), "WvertVel", VelBuffer[READ]);
		cShade.SetBuffer(cShade.FindKernel(_SpawnKernelName), "WvertDat", IdAgeBuffer[READ]);

        spawnCount += Mathf.Abs(psystem.particlesToSpawnPerFrame)*this.simSpeed;
        int intspawnCount = Mathf.FloorToInt(spawnCount);

        int toSpawnThisFrame = (int)Mathf.Min(intspawnCount, (int)(this.deadParticles / 64));

		if(toSpawnThisFrame*64 > this.deadParticles)
		{
			//Debug.Log("NOSPAWN>> ded:" + this.deadParticles +  "  	  liv:" + this.liveParticles + "  	  tot:" + (this.liveParticles + this.deadParticles));
			toSpawnThisFrame = 0;//(int)Mathf.Max(this.particlesLeftInBank - psystem.particlesToSpawnPerFrame*2,0);
		}
        else
        {
            spawnCount -= intspawnCount;
        }


		if(this.deadParticles + this.liveParticles != this.count && Time.frameCount > 2)
		{
			Debug.LogError("ERROR!!! PARTICLE COUNT OFFSET!!! ded:" + this.deadParticles +  "  	  liv:" + this.liveParticles + "  	  tot:" + (this.liveParticles + this.deadParticles));
		}
		if(toSpawnThisFrame > 0)
		{
			//Debug.Log("Testey>> ded:" + this.deadParticles +  "  \t  liv:" + this.liveParticles + "  \t  tot:" + (this.liveParticles + this.deadParticles));
			cShade.Dispatch(cShade.FindKernel(_SpawnKernelName), toSpawnThisFrame,1,1);
		}

		DisplayFrameSpawns(toSpawnThisFrame);

	}

	int[] lastframespawns = new int[10];

	void DisplayFrameSpawns(int numspawned)
	{

		int displaynum = numspawned*2;
		for(int i =lastframespawns.Length - 1; i > 0; i--)
		{
			displaynum += lastframespawns[i-1];
			lastframespawns[i] = lastframespawns[i-1];
		}

		lastframespawns[0] = numspawned;

		displaynum /= (lastframespawns.Length+1);

		actualspawn.text = "  Actual Spawned:(" + displaynum + ") x64 = " +displaynum*64 ; 
	}




	void DoSimming()
	{
		
		//SET THE BUFFERS FOR THE SIM:
		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "WvertDat", IdAgeBuffer[WRITE]);
		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "RvertDat", IdAgeBuffer[READ]);

		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "RvertPos", PosBuffer[READ]);
		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "RvertVel", VelBuffer[READ]);
		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "WvertPos", PosBuffer[WRITE]);
		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "WvertVel", VelBuffer[WRITE]);

		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "AliveList", LiveBuffer);		
		cShade.SetBuffer(cShade.FindKernel(_SimKernelName), "AdeadList", DeadBuffer);


		//RUN THE SIM
		cShade.Dispatch(cShade.FindKernel(_SimKernelName), count/64, 1, 1);

	}



	void OnRenderObject()
	{

		int[] args = GetArgs(LiveBuffer, argBuffer);

		this.liveParticles = args[0];
		int[] dedargs = GetArgs(DeadBuffer, deadBuffArgBuff);
		this.deadParticles = dedargs[0];


        lightHaloMat.SetPass(0);
        lightHaloMat.SetBuffer("_VertPos", PosBuffer[READ]);
        lightHaloMat.SetBuffer("_VertVel", VelBuffer[READ]);
        lightHaloMat.SetBuffer("_VertDat", IdAgeBuffer[READ]);
        lightHaloMat.SetBuffer("_LivingID", LiveBuffer);

        Graphics.DrawProceduralIndirect(MeshTopology.Points, argBuffer);

        material.SetPass(0);
        material.SetBuffer("_VertPos", PosBuffer[READ]);
        material.SetBuffer("_VertVel", VelBuffer[READ]);
        material.SetBuffer("_VertDat", IdAgeBuffer[READ]);
        material.SetBuffer("_LivingID", LiveBuffer);

        Graphics.DrawProceduralIndirect(MeshTopology.Points, argBuffer);

	}
	
	void OnDestroy()
	{
		PosBuffer[READ].Release();
		VelBuffer[READ].Release();
		PosBuffer[WRITE].Release();
		VelBuffer[WRITE].Release();

		IdAgeBuffer[WRITE].Release();
		IdAgeBuffer[READ].Release();

		argBuffer.Release();
		this.deadBuffArgBuff.Release();

		DeadBuffer.Release();
		LiveBuffer.Release();

	}





}