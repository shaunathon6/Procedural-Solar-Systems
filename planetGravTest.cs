using UnityEngine;
using System.Threading;
using System.Collections;

public class planetGravTest : MonoBehaviour
{
	// These values are public because they are set by the planet spawner
	// Mass is public because it is accessed by the planet's moons
	public float orbitDistance;
	public float sunMass;
	public float size;
	public float mass;
	public int numMoons;

	public float gravDist;
	
	string label;
	
	float orbitAngularVel;
	float temperature;
	float atmosphere;
	float spinSpeed;
	float gravity;
	float density;
	float volume;
	
	int texWidth;
	int texHeight;
	
	public Material gasMat;
	public Material planetMat;
	public Material atmosphereMat;
	public Mesh sphereMesh;
	
	Texture2D texture;
	Texture2D specular;
	Texture2D cloudLayer;
	
	Color[] gradient;
	int gradientSize = 100;
	
	Color left;
	Color right;
	
	float waves;				// Used for gas giants
	public float seaHeight; 			// Used for rock planets
	
	float maxNoise;
	float minNoise;
	float stretch;
	
	int[] p = new int[512];		// Permutations for noise
	
	Camera mainCam;
	
	GameObject atmosphereObject;
	
	bool infoCard = false; 		// Show planet info when true
	
	public GUISkin gui;
	public Texture2D smallCard;
	public Texture2D largeCard;


	void Start ()
	{
		// Give Density and calculate vol & mass
		density = Random.Range(3.0f,8.0f);
		size = Random.Range(0.8f,4.0f);
		volume = 4f*Mathf.PI*Mathf.Pow(size,3f)/3f;
		mass = density*volume;
		rigidbody.mass = mass;

		gravDist = Mathf.Sqrt((0.2f*mass)/0.05f);

		SphereCollider grav = gameObject.AddComponent<SphereCollider>();
		grav.radius = gravDist;
		grav.isTrigger = true;
		
		gravity = (0.2f*mass)/(size*size);
		
		float sunSize = Random.Range(6.0f,12.0f);
		
		float StefanBoltzmannConstant = 5.6703f*Mathf.Pow(10.0f,-9.0f);
		
		// Calc temp based on distance
		temperature = Mathf.Pow(sunSize/(10.0f*Mathf.PI*StefanBoltzmannConstant*Mathf.Pow(orbitDistance,2.0f)),0.5f);
		
		// Calc atmosphere. Stronger gravity holds more atmosphere
		atmosphere = Random.Range(0.1f,1.0f)*Mathf.Sqrt(gravity/Mathf.Sqrt(gravity));
		
		// Density of atmosphere influences temperature due to greenhouse effect
		temperature *= atmosphere + 0.5f;
		
		seaHeight = Mathf.Pow(Mathf.Clamp(0.5f*(1f-((temperature - 300f)/400f)), 0f, 0.9f), 3f)*atmosphere;
		
		waves = Random.Range(8.0f, 14.0f);
		
		// Calculate angular velocity = sqrt(G*(M+m)/r^3) real world G = 6.67e-11
		orbitAngularVel = Mathf.Sqrt((20.0f * (sunMass + mass))/(Mathf.Pow(orbitDistance, 3)));
		
		// Give the planet a random spin
		spinSpeed = Random.Range(-0.3f,-3.0f);

		rigidbody.AddTorque(new Vector3(0.0f, spinSpeed*10.0f, 0.0f));
		
		// Start at a random positio around the sun
		transform.RotateAround(Vector3.zero, Vector3.down, Random.Range(0.0f,359.9f));
		
		// Fill empty
		for (int i = 0; i < 256; i++) p[i] = -1;
		
		// Generate random numbers
		// P must contain exactly 2 of each int from 0-255
		// Both equal values must be 256 apart
		for (int i = 0; i < 256; i++) {
			while (true) {
				int iP = Random.Range(0, 256);
				if (p[iP] == -1) {
					p[iP] = p[iP+256] = i;
					break;
				}
			}
		}
		
		texHeight = Mathf.FloorToInt(size*200);
		texWidth = texHeight*2;
		
		float[,] noiseMap = new float[texWidth,texHeight];
		
		float noiseVal,x,y,z;
		
		float oneByteX = 1.0f/(float)texWidth;
		float oneByteY = 1.0f/(float)texHeight;
		
		float	phi,	sinPhi,		cosPhi,
		theta,	sinTheta,	cosTheta;
		
		for(int h = 0; h < texHeight; h++) {
			theta = (float)h * oneByteY * Mathf.PI;
			sinTheta = Mathf.Sin(theta);
			cosTheta = Mathf.Cos(theta);
			
			for(int w = 0; w < texWidth; w++) {
				phi = (((float)w * oneByteX * 2.0f) - 1.0f) * Mathf.PI;
				sinPhi = Mathf.Sin(phi);
				cosPhi = Mathf.Cos(phi);
				
				x = cosPhi*sinTheta;
				y = sinPhi*sinTheta;
				z = cosTheta;
				
				if(density > 3.0f)
					noiseVal = fractalSum(x, y, z, 10.0f);
				
				else
					noiseVal = fractalSine(x, y, z, 10.0f);
				
				maxNoise = Mathf.Max(maxNoise, noiseVal);
				minNoise = Mathf.Min(minNoise, noiseVal);
				
				noiseMap[w,h] = noiseVal;
			}
		}
		
		stretch = (maxNoise - minNoise);
		
		if (density > 3.0f)
		{
			planetTexture (ref noiseMap);
			createAtmosphere(ref noiseMap);
		}
		
		else
			gasTexture(ref noiseMap);
		
		mainCam = GameObject.Find("Main Camera").camera;
		

		
		// Assign label
		if(density < 3.0f)
			label = "Gas Giant";
		
		else if(temperature < 280f)
			label = "\t\tCold Planet";
		
		else if(temperature > 330f)
			label = "\t\tHot Planet";
		
		else if(seaHeight > 0.1f) 
			label = "Habitable Planet";
		
		else
			label = "Baren planet";
		
	}
	
	void gasTexture(ref float[,] noiseMap)
	{
		float noiseVal;
		
		generateGasGradient(3);
		
		texture = new Texture2D(texWidth, texHeight);
		renderer.material = gasMat;
		
		// Scale noise so it lies between 0 and 1
		// Then apply pixel colour
		for(int w = 0; w < texWidth; w++) {
			for(int h = 0; h < texHeight; h++) {
				noiseVal = (noiseMap[w,h] - minNoise) / stretch;
				
				int sample = Mathf.Clamp(Mathf.FloorToInt(noiseVal*gradientSize), 0, gradientSize-1);
				texture.SetPixel(w, h, gradient[sample]);
			}
		}
		
		texture.Apply();
		renderer.material.SetTexture("_MainTex", texture);
		
		// Scale the planet
		// Don't want to change local scale so this function will only scale the mesh
		Mesh mesh = GetComponent<MeshFilter>().mesh; 		// Get the mesh
		Vector3[] verts = new Vector3[mesh.vertexCount];
		verts = mesh.vertices; 								// Get the vertices
		
		for(int v = 0; v < mesh.vertexCount; v++) 			// Move through vertices
			verts[v] *= size; 								// Displace the vertex
		
		mesh.vertices = verts; 								// Put them back in the mesh
	}
	
	void planetTexture (ref float[,] noiseMap)
	{
		float noiseVal;
		
		generatePlanetGradient(Mathf.FloorToInt(seaHeight*gradientSize), 4);
		
		texture = new Texture2D(texWidth, texHeight);
		specular = new Texture2D(texWidth, texHeight);
		
		renderer.material = planetMat;
		
		// Scale noise so it lies between 0 and 1
		// Then apply pixel colour
		for(int w = 0; w < texWidth; w++) {
			for(int h = 0; h < texHeight; h++) {
				noiseVal = (noiseMap[w,h] - minNoise) / stretch;
				
				int sample = Mathf.Clamp(Mathf.FloorToInt(noiseVal*gradientSize), 0, gradientSize-1);
				texture.SetPixel(w, h, gradient[sample]);
				
				// Set pixel on the specular map
				if(noiseVal > seaHeight) 
					specular.SetPixel(w, h, Color.black);
				
				else 
					specular.SetPixel(w, h, Color.white);
				
			}
		}
		
		texture.Apply();
		specular.Apply();
		
		renderer.material.SetTexture("_MainTex", texture);
		renderer.material.SetTexture("_SpecularTex", specular);
		
		float x, y, z;
		float phi, theta;
		int U, V; // noisemap coords to find height scale;
		
		// Scale the planet
		// Don't want to local scale so this function will only scale the mesh
		Mesh mesh = GetComponent<MeshFilter>().mesh; 		// Get the mesh
		Vector3[] verts = new Vector3[mesh.vertexCount];
		verts = mesh.vertices; 								// Get the vertices
		
		for(int v = 0; v < mesh.vertexCount; v++)			// Move through vertices
		{
			// Get coords of vertex
			// Things are mirrored and swapped due to how I exported the mesh in blender
			x = -1f*verts[v].x;
			y = -1f*verts[v].z;
			z = verts[v].y;
			
			theta = Mathf.Acos (z / 0.5f);
			phi = Mathf.Atan2 (y,x);
			
			// Sometimes Atan2 returns negative angles, add 360 to get the positive of it
			if(phi < 0.0f)
				phi += Mathf.PI*2.0f;
			
			U = Mathf.FloorToInt (texWidth*(phi/(2f*Mathf.PI)));
			V = Mathf.FloorToInt (texHeight*((Mathf.PI - theta) / Mathf.PI));
			
			float scaleHeight = (noiseMap[U,V] - minNoise) / stretch;
			
			// Any land below the height of sea is set to the seaheight
			if(scaleHeight < seaHeight)
				scaleHeight = seaHeight;
			
			verts[v] *= (size + (scaleHeight/3.0f)); 		// Displace the vertex
		}
		
		mesh.vertices = verts; 								// Put them back in the mesh
	}
	
	void generatePlanetGradient(int sea, int exemplars)
	{
		int Hue, seaHue;
		float Sat, Val, prevVal;
		
		gradient = new Color[gradientSize];
		
		for(int i = 0; i < gradientSize; i++) gradient[i] = Color.clear;
		
		// Set leftmost sea exemplar
		seaHue = Hue = Random.Range(0,360);
		Sat = Random.Range(0.5f, 0.8f);
		Val = Random.Range(0.5f, 0.7f);
		gradient[0] = HSVtoRGB(Hue,Sat,Val);
		
		gradient[Mathf.FloorToInt(sea*0.75f)] = HSVtoRGB(Hue,Sat,Val);
		
		// Set rightmost sea exemplar (Use the same Hue)
		Sat = Random.Range(0.3f, 0.6f);
		Val += 0.2f;
		gradient[sea] = HSVtoRGB(Hue,Sat,Val);
		
		
		do 	// Set leftmost land exemplar
		{	Hue = Random.Range(0,360); 
		} 	while((Mathf.Abs(Hue - seaHue) <  60) || (Mathf.Abs(Hue - seaHue) > 300));
		
		Sat = Random.Range(0.3f, 0.8f);
		Val = Random.Range(0.2f, 0.3f);
		gradient[sea+1] = HSVtoRGB(Hue,Sat,Val);
		
		prevVal = Val;
		
		do 	// Set rightmost land exemplar
		{	Hue = Random.Range(0,360); 
		} 	while((Mathf.Abs(Hue - seaHue) <  60) || (Mathf.Abs(Hue - seaHue) > 300));
		
		Sat = Random.Range(0.1f, 0.6f);
		Val = Random.Range(0.3f, 0.9f);
		gradient[gradientSize-1] = HSVtoRGB(Hue,Sat,Val);
		
		// Set the rest of the exemplars
		for(int i = 0; i < exemplars-2 ; i++) {
			while(true) {
				
				int iE = Random.Range(sea+1,gradientSize);
				
				// Make sure new exemplar is not close to or on top of another one
				if((gradient[iE]   == Color.clear) && 
				   (gradient[iE+1] == Color.clear) &&
				   (gradient[iE-1] == Color.clear) &&
				   (gradient[iE+2] == Color.clear) &&
				   (gradient[iE-2] == Color.clear)) {
					
					do
					{	Hue = Random.Range(0,360); 
					} 	while((Mathf.Abs(Hue - seaHue) <  60) || (Mathf.Abs(Hue - seaHue) > 300));
					
					do
					{	Val = Random.Range(0.3f, 0.7f);
					}	while(Mathf.Abs(Val - prevVal) < 0.2f);
					prevVal = Val;
					
					Sat = Random.Range(0.2f, 0.8f);
					
					gradient[iE] = HSVtoRGB(Hue,Sat,Val);
					
					break;
				}
			}
		}
		
		// Interpolate remaining colour values
		for(int i = 1; i < gradientSize-1; i++) {
			if(gradient[i] == Color.clear) {
				left = gradient[i-1];
				int j;
				for(j = i+1; j < gradientSize; j++){
					right = gradient[j];
					if(right != Color.clear)
						break;
				} 
				
				float dist = j - i;
				float ratio = dist/(dist+1.0f);
				
				gradient[i] = Color.Lerp(right, left, ratio);
			}
		}
	}
	
	void generateGasGradient(int exemplars)
	{
		int Hue;
		float Sat, Val;
		
		gradient = new Color[gradientSize];
		
		for(int i = 0; i < gradientSize; i++) gradient[i] = Color.clear;
		
		// Set leftmost exemplar
		Hue = Random.Range(0,360); 
		Sat = Random.Range(0.3f, 0.6f);
		Val = Random.Range(0.5f, 0.9f);
		gradient[0] = HSVtoRGB(Hue,Sat,Val);
		
		// Set rightmost exemplar
		Hue = Random.Range(0,360); 
		Sat = Random.Range(0.1f, 0.4f);
		Val = Random.Range(0.3f, 0.5f);
		gradient[gradientSize-1] = HSVtoRGB(Hue,Sat,Val);
		
		// Set the rest of the exemplars
		for(int i = 0; i < exemplars-2 ; i++) {
			while(true) {
				
				int iE = Random.Range(0,gradientSize);
				
				// Make sure new exemplar is not close to or on top of another one
				if((gradient[iE]   == Color.clear) && 
				   (gradient[iE+1] == Color.clear) &&
				   (gradient[iE+2] == Color.clear) &&
				   (gradient[iE-1] == Color.clear) &&
				   (gradient[iE-2] == Color.clear)) {
					
					Hue = Random.Range(0,360); 
					Sat = Random.Range(0.2f, 0.7f);
					Val = Random.Range(0.2f, 0.7f);
					gradient[iE] = HSVtoRGB(Hue,Sat,Val);
					
					break;
				}
			}
		}
		
		// Interpolate remaining colour values
		for(int i = 1; i < gradientSize-1; i++) {
			if(gradient[i] == Color.clear) {
				left = gradient[i-1];
				int j;
				for(j = i+1; j < gradientSize; j++){
					right = gradient[j];
					if(right != Color.clear)
						break;
				} 
				
				float dist = j - i;
				float ratio = dist/(dist+1.0f);
				
				gradient[i] = Color.Lerp(right, left, ratio);
			}
		}
	}
	
	void createAtmosphere(ref float[,] noiseMap)
	{
		cloudLayer = new Texture2D (texWidth, texHeight);
		Color cloudPixel = new Color (0.8f, 0.8f, 0.8f, 0.0f);
		float noiseVal;
		
		// Calculate alpha value for each pixel
		for (int u = 0; u < texWidth; u++) {
			for (int v = 0; v < texHeight; v++) {
				noiseVal = (noiseMap[u,v] - minNoise) / stretch;
				
				cloudPixel.a = Mathf.Pow(noiseVal, 1f/atmosphere) - (0.3f/atmosphere);
				
				cloudLayer.SetPixel(u,v,cloudPixel);
			}
		}
		
		cloudLayer.Apply ();
		
		// Create GameObject for atmosphere layer
		atmosphereObject = new GameObject(name);
		MeshFilter meshFilter = atmosphereObject.AddComponent<MeshFilter>();
		atmosphereObject.AddComponent<MeshRenderer>();
		meshFilter.sharedMesh = sphereMesh;
		atmosphereObject.renderer.material = atmosphereMat;
		atmosphereObject.transform.position = transform.position;
		atmosphereObject.renderer.material.mainTexture = cloudLayer;
		atmosphereObject.transform.parent = transform;
		atmosphereObject.transform.localScale *= size + 0.35f;
		atmosphereObject.transform.Rotate(new Vector3(30f, 0f, 45f));
	}
	
	float fractalSum (float x , float y, float z,float oct)
	{
		float val = 0.0f;
		
		for(int i = 1; i <= oct; i++) {
			var sq = Mathf.Pow(2,i);
			val += (1/sq) * Mathf.Abs(noise(sq * x * size * 0.5f,
			                                sq * y * size * 0.5f,
			                                sq * z * size * 0.5f));
		}
		return val/oct;
	}
	
	float fractalSine (float x , float y, float z,float oct)
	{
		float val = 0.0f;
		
		for(int i = 2; i <= oct+1; i++) {
			var sq = Mathf.Pow(2,i);
			val += (1/sq) * noise(sq * x, sq * y, sq * z);
		}
		
		val *= oct*0.5f;
		return Mathf.Sin((waves*z) + val);
	}
	
	void Update ()
	{
		if(atmosphereObject)
			atmosphereObject.transform.Rotate(new Vector3(0f, -2.5f*spinSpeed*Time.deltaTime, 0f));
		
		//transform.Rotate(new Vector3(0f, spinSpeed*Time.deltaTime, 0f));
		//transform.RotateAround(Vector3.zero, Vector3.down, orbitAngularVel * Time.deltaTime);
	}

	void OnTriggerEnter(Collider other)
	{
		other.transform.parent = gameObject.transform;
	}

	/*
	void OnTriggerStay(Collider other)
	{
		Vector3 forceDir = transform.position - other.transform.position;
		float dist = forceDir.magnitude;
		forceDir = forceDir.normalized;
		Debug.DrawRay(other.transform.position, forceDir, Color.red);

		float force = 0.2f * rigidbody.mass * other.rigidbody.mass / dist;
		//Debug.Log("Force = " + force);
		other.rigidbody.AddForce(forceDir * force);
	}
	*/

	void OnCollisionEnter(Collision other)
	{
		Debug.Log("BOOP ");
		other.transform.parent = gameObject.transform;
		Destroy(other.rigidbody);
		Destroy (other.gameObject.GetComponent<TrailRenderer>());
	}
	
	// Display planet information
	void OnGUI()
	{
		GUI.skin = gui;
		
		if(density > 3.0f)
		{
			if(infoCard == true && mainCam.GetComponent<solarsysCamera>().orbitType == transform.name)
			{
				GUI.skin.box.normal.background = largeCard;
				
				GUI.Box(new Rect(Screen.width - 350, Screen.height/2f - 210, 280, 420),
				        "\n\t\t\t\t" 					+ label +
				        
				        "\n\nRadius:\t\t\t\t   "		+ (100f*size).ToString("F2") + " km" +
				        "\nMass:\t\t\t\t"				+ mass.ToString("F2") + "e24 kg" +
				        "\nGravity:\t\t\t\t\t\t "		+ gravity.ToString("F2") + " m/s" +
				        
				        "\n\nLength of day:\t\t\t\t\t\t"+ (-1f/spinSpeed).ToString("F2") +
				        "\nLength of year:\t\t\t\t\t\t"	+ (0.2f*Mathf.PI/orbitAngularVel).ToString("F2") +
				        "\nDistance from sun:\t\t"		+ orbitDistance.ToString("F2") +
				        
				        "\n\nTemperature:\t\t\t\t "		+ temperature.ToString("F2") + "K" +
				        "\nAtmosphere:\t\t\t\t\t\t\t"	+ atmosphere.ToString("F2") +
				        "\nNumber of moons:\t\t\t\t\t"	+ numMoons);
				
				if(GUI.Button(new Rect(Screen.width - 72, Screen.height/2f - 196, 40, 40), "X"))
					infoCard = false;
			}
			
			else if(infoCard == true)
			{
				GUI.skin.box.normal.background = smallCard;
				
				Vector2 offset = mainCam.WorldToScreenPoint(transform.position);
				if(renderer.isVisible)
				{
					GUI.Box(new Rect(offset.x + 50, Screen.height - offset.y - 80, 180, 250),
					        "\n\t\t\t\t\t" 				+ label +
					        
					        "\n\nRadius:\t\t\t\t"		+ (100f*size).ToString("F2") + " km" +
					        "\nDistance:\t\t\t\t\t"		+ orbitDistance.ToString("F2") + "km" +
					        "\nTemperature:\t\t\t "		+ temperature.ToString("F2") + " K" +
					        "\nAtmosphere:\t\t\t\t\t\t "+ atmosphere.ToString("F2"));
					
					if(GUI.Button(new Rect(offset.x + 328, Screen.height - offset.y - 67, 40, 40), "X"))
						infoCard = false;
					
					if(GUI.Button(new Rect(offset.x + 250, Screen.height - offset.y + 148, 90, 20), "Zoom"))
					{
						mainCam.transform.parent = transform;
						mainCam.GetComponent<solarsysCamera>().orbitType = transform.name;
						mainCam.GetComponent<solarsysCamera>().reset = true;
						
						transform.localScale = Vector3.one;
					}
				}
			}
		}
		
		else
		{
			if(infoCard == true && mainCam.GetComponent<solarsysCamera>().orbitType == transform.name)
			{
				GUI.skin.box.normal.background = largeCard;
				
				GUI.Box(new Rect(Screen.width - 350, Screen.height/2f - 182, 280, 364),
				        "\t\t\t\t\t\t\t\t" 				+ label +
				        
				        "\n\nRadius:\t\t\t\t   "		+ (100f*size).ToString("F2") + " km" +
				        "\nMass:\t\t\t\t"				+ mass.ToString("F2") + "e24 kg" +
				        "\nGravity:\t\t\t\t\t\t "		+ gravity.ToString("F2") + " m/s" +
				        
				        "\n\nLength of day:\t\t\t\t\t\t"+ (-1f/spinSpeed).ToString("F2") +
				        "\nLength of year:\t\t\t\t\t\t"	+ (0.2f*Mathf.PI/orbitAngularVel).ToString("F2") +
				        "\nDistance from sun:\t\t"		+ orbitDistance.ToString("F2") +
				        
				        "\n\nTemperature:\t\t\t\t"		+ temperature.ToString("F2") + "K" +
				        "\nNumber of moons:\t\t\t\t\t"	+ numMoons);
				
				if(GUI.Button(new Rect(Screen.width - 72, Screen.height/2f - 170, 40, 40), "X"))
					infoCard = false;
			}
			
			else if(infoCard == true)
			{
				GUI.skin.box.normal.background = smallCard;
				
				Vector2 offset = mainCam.WorldToScreenPoint(transform.position);
				if(renderer.isVisible)
				{
					GUI.Box(new Rect(offset.x + 50, Screen.height - offset.y - 80, 180, 225),
					        "\n\t\t\t\t\t\t\t\t" 			+ label +
					        
					        "\n\nRadius:\t\t\t\t"		+ (100f*size).ToString("F2") + " km" +
					        "\nDistance:\t\t\t\t\t"		+ orbitDistance.ToString("F2") + "km" +
					        "\nTemperature:\t\t\t "		+ temperature.ToString("F2") + " K");
					
					if(GUI.Button(new Rect(offset.x + 328, Screen.height - offset.y - 67, 40, 40), "X"))
						infoCard = false;
					
					if(GUI.Button(new Rect(offset.x + 250, Screen.height - offset.y + 120, 90, 20), "Zoom"))
					{
						mainCam.transform.parent = transform;
						mainCam.GetComponent<solarsysCamera>().orbitType = transform.name;
						mainCam.GetComponent<solarsysCamera>().reset = true;
						
						transform.localScale = Vector3.one;
					}
				}
			}
			
		}
	}
	
	float noise(float x, float y, float z) {
		int X = (int)Mathf.Floor(x) & 255,							// FIND UNITY CUBE THAT
		Y = (int)Mathf.Floor(y) & 255,							// CONTAINS POINT
		Z = (int)Mathf.Floor(z) & 255;
		
		x -= (int)Mathf.Floor(x);									// FIND RELATIVE X,Y,Z
		y -= (int)Mathf.Floor(y);									// OF POINT IN CUBE
		z -= (int)Mathf.Floor(z);
		
		float u = fade(x); float v = fade(y); float w = fade(z);	// COMPUTE FADE CURVES
		
		int A = p[X  ]+Y; int AA = p[A]+Z; int AB = p[A+1]+Z;		// HASH COORDINATES OF
		int B = p[X+1]+Y; int BA = p[B]+Z; int BB = p[B+1]+Z;		// THE 8 CUBE CORNERS,
		
		return lerp(w, 	lerp(v, lerp(u, grad(p[AA  ], x  , y  , z   ),  		// AND ADD
		                             grad(p[BA  ], x-1, y  , z   )), 		// BLENDED
		                     lerp(u, grad(p[AB  ], x  , y-1, z   ),  		// RESULTS
		     grad(p[BB  ], x-1, y-1, z   ))),		// FROM  8
		            lerp(v, lerp(u, grad(p[AA+1], x  , y  , z-1 ),  		// CORNERS
		             grad(p[BA+1], x-1, y  , z-1 )), 		// OF CUBE
		     lerp(u,	grad(p[AB+1], x  , y-1, z-1 ),
		     grad(p[BB+1], x-1, y-1, z-1 ))));
	}
	
	float fade(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }
	float lerp(float t, float a, float b) { return a + t * (b - a); }
	float grad(int hash, float x, float y, float z) {
		int h = hash & 15;                      		// CONVERT LO 4 BITS OF HASH CODE
		float u = h<8 ? x : y;							// INTO 12 GRADIENT DIRECTIONS.
		float v = h<4 ? y : h==12||h==14 ? x : z;
		return ((h&1) == 0 ? u : -u) + ((h&2) == 0 ? v : -v);
	}
	
	// Convert HSV to RGB
	Color HSVtoRGB(float h, float s, float v)
	{
		int i;
		float f, p, q, t;
		
		if(s == 0) return Color.white*v;
		
		float hh = h/60f;	// sector 0 to 5
		
		i = (int)Mathf.Floor(hh);
		f = hh - i;			// factorial part of h
		
		p = v * (1 - s);
		q = v * (1 - s * f);
		t = v * (1 - s * (1 - f));
		
		if(i==0) return new Color(v,t,p);
		if(i==1) return new Color(q,v,p);
		if(i==2) return new Color(p,v,t);
		if(i==3) return new Color(p,q,v);
		if(i==4) return new Color(t,p,v);
		if(i==5) return new Color(v,p,q);
		
		return Color.magenta; // Debug
	}
}
