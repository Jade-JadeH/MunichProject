using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using Esri.GameEngine.View;
using UnityEngine.Profiling;
using UnityEditor;

public class featureLayer : MonoBehaviour
{
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	[SerializeField] private string featureServiceUrl = "https://services1.arcgis.com/6677msI40mnLuuLr/arcgis/rest/services/MunichTrees3857/FeatureServer/0/query";

	
	public GameObject treePrefab;
	public ArcGISMapComponent arcGISMapComponent;
	public float sceneScale = 0.001f;   
	public int debugPrintLimit = 20;
	private ArcGISSpatialReference sr;
	private const int batchSize = 2000;
	private ArcGISView View;

	IEnumerator Start()
    {
		sr= arcGISMapComponent.Map.SpatialReference;
		yield return LoadAllTrees();

		View = arcGISMapComponent.View;
		long systemMemMiB = 256; // 2 GB
		long gpuMemMiB = 256;    // 1 GB
		View.SetMemoryQuotas(systemMemMiB, gpuMemMiB);
	}


	IEnumerator LoadAllTrees()
	{
		int offset = 0;
		int totalCount = 0;

		while (true)
		{
			string query = featureServiceUrl +
				"?where=1=1" +
				"&outFields=*" +
				"&returnGeometry=true" +
				"&outSR=3857" +
				$"&resultOffset={offset}&resultRecordCount={batchSize}" +
				"&f=json";

			using (UnityWebRequest req = UnityWebRequest.Get(query))
			{
				yield return req.SendWebRequest();

				if (req.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError("Query failed: " + req.error);
					yield break;
				}

				JObject json = JObject.Parse(req.downloadHandler.text);
				JArray features = (JArray)json["features"];
				if (features == null || features.Count == 0)
				{
					Debug.Log($"Finished loading all features. Total: {totalCount}");
					break; 
				}

				int batchCount = 0;
				foreach (var f in features)
				{
					var geom = f["geometry"];
					if (geom == null) continue;

					double x = geom["x"].Value<double>();
					double y = geom["y"].Value<double>();
					var attrs = f["attributes"];
					float height = attrs?["baumhoehe"]?.Value<float>() ?? 10f; 
					float diameter = Mathf.Clamp(height * 0.1f, 0.2f, 3f);

					var point = new ArcGISPoint(x, y, 0, sr);
					var tree = Instantiate(treePrefab, Vector3.zero, Quaternion.identity, transform);

					Transform model = tree.transform.childCount > 0 ? tree.transform.GetChild(0) : tree.transform;


					model.localScale = new Vector3(diameter/2, height / 15f, diameter/2);
					//model.localScale = new Vector3(diameter / 36f, height / 75f, diameter / 36f);

					var location = tree.AddComponent<ArcGISLocationComponent>();
					location.Position = point;
					location.SurfacePlacementMode = ArcGISSurfacePlacementMode.OnTheGround;

					Renderer billboardRenderer = tree.transform.Find("Billboard")?.GetComponent<Renderer>();
					if (billboardRenderer != null)
					{
						
						Material mat = new Material(billboardRenderer.sharedMaterial);

						Color[] fallColors = {
							new Color(0.85f, 0.45f, 0.10f),  
							new Color(0.95f, 0.70f, 0.20f),  
							new Color(0.80f, 0.30f, 0.15f),  
							new Color(0.60f, 0.35f, 0.10f),  
							new Color(0.50f, 0.60f, 0.25f),  
							new Color(0.75f, 0.55f, 0.15f),  
						};

						Color baseColor = fallColors[Random.Range(0, fallColors.Length)];

						
						float brightnessJitter = Random.Range(0.9f, 1.1f);
						baseColor *= brightnessJitter;


						mat.SetColor("_UnlitColor", baseColor);

						billboardRenderer.material = mat;
					}

					if (totalCount < debugPrintLimit)
					{
						Debug.Log($"Tree {totalCount}: x={x}, y={y}, diameter={diameter},height={height}");
						
					}
					batchCount++;
					totalCount++;
				}

				Debug.Log($"Loaded batch {offset / batchSize + 1}: {batchCount} features");
				offset += batchSize;

				yield return null;
			}
		}

		Debug.Log($"Total trees instantiated: {totalCount}");
	}

	// Update is called once per frame
	void Update()
    {
		/*Debug.Log($"Mono used: {Profiler.GetMonoUsedSizeLong() / (1024 * 1024)} MB");
		//Debug.Log($"Total reserved: {Profiler.GetTotalReservedMemoryLong() / (1024 * 1024)} MB");
		long totalMemory = Profiler.GetTotalAllocatedMemoryLong();
		long reservedMemory = Profiler.GetTotalReservedMemoryLong();
		long unusedReservedMemory = Profiler.GetTotalUnusedReservedMemoryLong();

		Debug.Log($"Allocated: {totalMemory / (1024 * 1024)} MB | Reserved: {reservedMemory / (1024 * 1024)} MB | Unused Reserved: {unusedReservedMemory / (1024 * 1024)} MB");*/
	}
}