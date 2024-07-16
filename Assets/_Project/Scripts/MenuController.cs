using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using System.Xml;
using UnityEngine.SceneManagement;
using System.Linq;

namespace ARLocation.MapboxRoutes.SampleProject
{
    public class MenuController : MonoBehaviour
    {
        public enum LineType
        {
            Route,
            NextTarget
        }

        public class DataEntry
        {
            public int id;
            public double lat;
            public double lng;
            public double altitude;
            public string altitudeMode;
            public string name;
            public string meshId;
            public float movementSmoothing;
            public int maxNumberOfLocationUpdates;
            public bool useMovingAverage;
            public bool hideObjectUtilItIsPlaced;

            public AltitudeMode getAltitudeMode()
            {
                if (altitudeMode == "GroundRelative")
                {
                    return AltitudeMode.GroundRelative;
                }
                else if (altitudeMode == "DeviceRelative")
                {
                    return AltitudeMode.DeviceRelative;
                }
                else if (altitudeMode == "Absolute")
                {
                    return AltitudeMode.Absolute;
                }
                else
                {
                    return AltitudeMode.Ignore;
                }
            }
        }

        public string MapboxToken = "pk.eyJ1IjoiZG1iZm0iLCJhIjoiY2tyYW9hdGMwNGt6dTJ2bzhieDg3NGJxNyJ9.qaQsMUbyu4iARFe0XB2SWg";
        public GameObject ARSession;
        public GameObject ARSessionOrigin;
        public GameObject RouteContainer;
        public Camera Camera;
        public Camera MapboxMapCamera;
        public MapboxRoute MapboxRoute;
        public AbstractRouteRenderer RoutePathRenderer;
        public AbstractRouteRenderer NextTargetPathRenderer;
        public Texture RenderTexture;
        public Mapbox.Unity.Map.AbstractMap Map;
        [Range(100, 800)]
        public int MapSize = 400;
        public DirectionsFactory DirectionsFactory;
        public int MinimapLayer;
        public Material MinimapLineMaterial;
        public float BaseLineWidth = 2;
        public float MinimapStepSize = 0.5f;

        public PrefabDatabase PrefabDatabase;
        public TextAsset XmlDataFile;
        // private List<DataEntry> _dataEntries = new List<DataEntry>();
        // private List<GeocodingFeature> results;

        public bool DebugMode;
        public List<PlaceAtLocation> Instances
        {
            get => _placeAtComponents;
        }
        private List<DataEntry> _dataEntries = new List<DataEntry>();
        private List<PlaceAtLocation> _placeAtComponents = new List<PlaceAtLocation>();

        private AbstractRouteRenderer currentPathRenderer => s.LineType == LineType.Route ? RoutePathRenderer : NextTargetPathRenderer;

        public LineType PathRendererType
        {
            get => s.LineType;
            set
            {
                if (value != s.LineType)
                {
                    currentPathRenderer.enabled = false;
                    s.LineType = value;
                    currentPathRenderer.enabled = true;

                    if (s.View == View.Route)
                    {
                        MapboxRoute.RoutePathRenderer = currentPathRenderer;
                    }
                }
            }
        }

        enum View
        {
            SearchMenu,
            Route,
        }

        [System.Serializable]
        private class State
        {
            public string QueryText = "";
            public List<GeocodingFeature> Results = new List<GeocodingFeature>();
            public View View = View.SearchMenu;
            public Location destination;
            public LineType LineType = LineType.NextTarget;
            public string ErrorMessage;
        }

        private State s = new State();

        private GUIStyle _textStyle;
        GUIStyle textStyle()
        {
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label);
                _textStyle.fontSize = 48;
                _textStyle.fontStyle = FontStyle.Bold;
            }

            return _textStyle;
        }

        private GUIStyle _textFieldStyle;
        GUIStyle textFieldStyle()
        {
            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(GUI.skin.textField);
                _textFieldStyle.fontSize = 48;
            }
            return _textFieldStyle;
        }

        private GUIStyle _errorLabelStyle;
        GUIStyle errorLabelSytle()
        {
            if (_errorLabelStyle == null)
            {
                _errorLabelStyle = new GUIStyle(GUI.skin.label);
                _errorLabelStyle.fontSize = 24;
                _errorLabelStyle.fontStyle = FontStyle.Bold;
                _errorLabelStyle.normal.textColor = Color.red;
            }

            return _errorLabelStyle;
        }


        private GUIStyle _buttonStyle;
        GUIStyle buttonStyle()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button);
                _buttonStyle.fontSize = 48;
            }

            return _buttonStyle;
        }

        void Awake()
        {
            // MapboxMapCamera.gameObject.SetActive(false);
            // Map.SetCenterLatitudeLongitude()
        }

        void Start()
        {
            NextTargetPathRenderer.enabled = false;
            RoutePathRenderer.enabled = false;
            ARLocationProvider.Instance.OnEnabled.AddListener(onLocationEnabled);
            Map.OnUpdated += OnMapRedrawn;
        }

        public void SetActiveGameObjects(bool value)
        {
            foreach (var i in _placeAtComponents)
            {
                i.gameObject.SetActive(value);
            }
        }

        public void HideMeshes()
        {
            foreach (var i in _placeAtComponents)
            {
                Utils.Misc.HideGameObject(i.gameObject);
            }
        }

        public void ShowMeshes()
        {
            foreach (var i in _placeAtComponents)
            {
                Utils.Misc.ShowGameObject(i.gameObject);
            }
        }

        private void OnMapRedrawn()
        {
            // Debug.Log("OnMapRedrawn");
            if (currentResponse != null)
            {
                buildMinimapRoute(currentResponse);
            }
        }

        private void onLocationEnabled(Location location)
        {
            Map.SetCenterLatitudeLongitude(new Mapbox.Utils.Vector2d(location.Latitude, location.Longitude));
            // Map.SetZoom(18);
            Map.UpdateMap();
        }

        void OnEnable()
        {
            Debug.Log("Enable!!!!!!!!");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            // ARLocationProvider.Instance.OnEnabled.RemoveListener(onLocationEnabled);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"Scene Loaded: {scene.name}");
        }

        void drawMap()
        {
            var tw = RenderTexture.width;
            var th = RenderTexture.height;

            var scale = MapSize / th;
            var newWidth = scale * tw;
            var x = Screen.width / 2 - newWidth / 2;
            float border;
            if (x < 0)
            {
                border = -x;
            }
            else
            {
                border = 0;
            }


            GUI.DrawTexture(new Rect(x, Screen.height - MapSize, newWidth, MapSize), RenderTexture, ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(new Rect(0, Screen.height - MapSize - 20, Screen.width, 20), separatorTexture, ScaleMode.StretchToFill, false);

            var newZoom = GUI.HorizontalSlider(new Rect(0, Screen.height - 60, Screen.width, 60), Map.Zoom, 10, 22);

            if (newZoom != Map.Zoom)
            {
                Map.SetZoom(newZoom);
                Map.UpdateMap();
                // buildMinimapRoute(currentResponse);
            }
        }

        void OnGUI()
        {
            // Mengambil informasi safe area
            Rect safeArea = Screen.safeArea;

            // Konversi safe area dari piksel layar ke unit GUI
            float safeAreaTop = Screen.height - (safeArea.y + safeArea.height);
            float safeAreaBottom = safeArea.y;
            float safeAreaLeft = safeArea.x;
            float safeAreaRight = Screen.width - (safeArea.x + safeArea.width);

            // Menambahkan padding untuk safe area
            GUILayout.BeginArea(new Rect(safeAreaLeft, safeAreaTop, safeArea.width, safeArea.height));

            if (s.View == View.Route)
            {
                drawMap();
                GUILayout.EndArea();
                return;
            }

            float h = Screen.height - MapSize;
            GUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(20, 20, 20, 20) }, GUILayout.MaxHeight(h), GUILayout.Height(h));

            var w = Screen.width;

            GUILayout.BeginVertical(GUILayout.MaxHeight(100));
            GUILayout.Label("Location Search", textStyle());
            GUILayout.BeginHorizontal(GUILayout.MaxHeight(100), GUILayout.MinHeight(100));
            s.QueryText = GUILayout.TextField(s.QueryText, textFieldStyle(), GUILayout.MinWidth(0.8f * w), GUILayout.MaxWidth(0.8f * w));

            if (GUILayout.Button("OK", buttonStyle(), GUILayout.MinWidth(0.15f * w), GUILayout.MaxWidth(0.15f * w)))
            {
                if (string.IsNullOrEmpty(s.QueryText))
                {
                    LoadDataFromXml();
                }
                else
                {
                    s.ErrorMessage = null;
                    StartCoroutine(search());
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            if (s.ErrorMessage != null)
            {
                GUILayout.Label(s.ErrorMessage, errorLabelSytle());
            }

            foreach (var r in s.Results)
            {
                if (GUILayout.Button(r.place_name, new GUIStyle(buttonStyle()) { alignment = TextAnchor.MiddleLeft, fontSize = 24, fixedHeight = 0.05f * Screen.height }))
                {
                    // Mengambil koordinat pertama dari geometry
                    if (r.geometry.coordinates.Count > 0)
                    {
                        Location dest = r.geometry.coordinates[0];
                        StartRoute(dest);
                        s.View = View.Route;  // Set the view to Route when an item is clicked

                        // BuildGameObjects();

                        PlacePrefabAtLocation(new DataEntry
                        {
                            lat = r.geometry.coordinates[0].Latitude,
                            lng = r.geometry.coordinates[0].Longitude,
                            altitude = 0, // Sesuaikan dengan altitude yang sesuai dari r.geometry.coordinates[0]
                            altitudeMode = "absolute", // Sesuaikan dengan mode altitude yang sesuai
                            meshId = "yourPrefabId", // Sesuaikan dengan ID prefab yang ingin ditampilkan
                            movementSmoothing = 0.5f, // Contoh smoothing
                            maxNumberOfLocationUpdates = 5, // Contoh jumlah maksimal pembaruan lokasi
                            useMovingAverage = true, // Contoh penggunaan rata-rata bergerak
                            hideObjectUtilItIsPlaced = true // Contoh menyembunyikan objek sampai ditempatkan
                        });
                    }
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndVertical();

            drawMap(); // Make sure drawMap is called here

            GUILayout.EndArea(); // Menutup area safe area
        }

        void LoadDataFromXml()
        {
            if (XmlDataFile == null)
            {
                Debug.LogError("Failed to load XML file from Resources folder.");
                return;
            }

            string xmlString = XmlDataFile.text;

            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(xmlString);
            }
            catch (XmlException e)
            {
                Debug.LogError("[MenuController]: Failed to parse XML file: " + e.Message);
                return;
            }

            // Mengambil root dan child nodes dari XML
            XmlNode root = xmlDoc.FirstChild;
            XmlNodeList nodes = root.ChildNodes;

            // Menginisialisasi List<GeocodingFeature> untuk menyimpan hasil dari XML
            s.Results.Clear();

            // Iterasi melalui setiap node dalam XML dan menambahkannya ke List<GeocodingFeature>
            foreach (XmlNode node in nodes)
            {
                try
                {
                    // string place_name = node["name"].InnerText; // Ubah sesuai dengan struktur XML Anda
                    // double lat = double.Parse(node["lat"].InnerText, CultureInfo.InvariantCulture);
                    // double lng = double.Parse(node["lng"].InnerText, CultureInfo.InvariantCulture);

                    int id = int.Parse(node["id"].InnerText);
                    double lat = double.Parse(node["lat"].InnerText, CultureInfo.InvariantCulture);
                    double lng = double.Parse(node["lng"].InnerText, CultureInfo.InvariantCulture);
                    double altitude = double.Parse(node["altitude"].InnerText, CultureInfo.InvariantCulture);
                    string altitudeMode = node["altitudeMode"].InnerText;
                    string place_name = node["name"].InnerText;
                    string meshId = node["meshId"].InnerText;
                    float movementSmoothing = float.Parse(node["movementSmoothing"].InnerText, CultureInfo.InvariantCulture);
                    int maxNumberOfLocationUpdates = int.Parse(node["maxNumberOfLocationUpdates"].InnerText);
                    bool useMovingAverage = bool.Parse(node["useMovingAverage"].InnerText);
                    bool hideObjectUtilItIsPlaced = bool.Parse(node["hideObjectUtilItIsPlaced"].InnerText);

                    Route.Geometry geometry = new Route.Geometry();
                    geometry.coordinates.Add(new Location(lat, lng, 0));

                    GeocodingFeature result = new GeocodingFeature
                    {
                        place_name = place_name,
                        geometry = geometry,
                        // Tambahkan properti lainnya jika diperlukan
                    };

                    DataEntry entry = new DataEntry()
                    {
                        id = id,
                        lat = lat,
                        lng = lng,
                        altitudeMode = altitudeMode,
                        altitude = altitude,
                        name = name,
                        meshId = meshId,
                        movementSmoothing = movementSmoothing,
                        maxNumberOfLocationUpdates = maxNumberOfLocationUpdates,
                        useMovingAverage = useMovingAverage,
                        hideObjectUtilItIsPlaced = hideObjectUtilItIsPlaced
                    };

                    _dataEntries.Add(entry);

                    s.Results.Add(result);
                }
                catch (XmlException e)
                {
                    Debug.LogError("[MenuController]: Failed to read node: " + e.Message);
                }
            }

            // Contoh debugging untuk memastikan data dimuat dengan benar
            Debug.Log("Number of results loaded: " + s.Results.Count);
            foreach (var result in s.Results)
            {
                Debug.Log("Result place_name: " + result.place_name);
            }
        }

        void BuildGameObjects()
        {
            foreach (var entry in _dataEntries)
            {
                var Prefab = PrefabDatabase.GetEntryById(entry.meshId);

                if (!Prefab)
                {
                    Debug.LogWarning($"[ARLocation#WebMapLoader]: Prefab {entry.meshId} not found.");
                    continue;
                }

                var PlacementOptions = new PlaceAtLocation.PlaceAtOptions()
                {
                    MovementSmoothing = entry.movementSmoothing,
                    MaxNumberOfLocationUpdates = entry.maxNumberOfLocationUpdates,
                    UseMovingAverage = entry.useMovingAverage,
                    HideObjectUntilItIsPlaced = entry.hideObjectUtilItIsPlaced
                };

                var location = new Location()
                {
                    Latitude = entry.lat,
                    Longitude = entry.lng,
                    Altitude = entry.altitude,
                    AltitudeMode = entry.getAltitudeMode(),
                    Label = entry.name
                };

                var instance = PlaceAtLocation.CreatePlacedInstance(Prefab,
                                                                    location,
                                                                    PlacementOptions,
                                                                    DebugMode);

                _placeAtComponents.Add(instance.GetComponent<PlaceAtLocation>());
            }
        }

        public void PlacePrefabAtLocation(DataEntry entry)
        {
            // Mendapatkan Prefab dari PrefabDatabase berdasarkan meshId atau ID unik lainnya dari DataEntry
            var prefab = PrefabDatabase.GetEntryById(entry.meshId);

            if (!prefab)
            {
                Debug.LogWarning($"Prefab {entry.meshId} not found in PrefabDatabase.");
                return;
            }

            // Membuat Location dari koordinat geografis DataEntry
            var location = new Location()
            {
                Latitude = entry.lat,
                Longitude = entry.lng,
                Altitude = entry.altitude,
                AltitudeMode = entry.getAltitudeMode(), // Sesuaikan dengan mode altitudenya
            };

            // Opsi untuk menempatkan prefab di lokasi
            var placeOptions = new PlaceAtLocation.PlaceAtOptions()
            {
                MovementSmoothing = entry.movementSmoothing, // Contoh smoothing
                MaxNumberOfLocationUpdates = entry.maxNumberOfLocationUpdates, // Contoh jumlah maksimal pembaruan lokasi
                UseMovingAverage = entry.useMovingAverage, // Contoh penggunaan rata-rata bergerak
                HideObjectUntilItIsPlaced = entry.hideObjectUtilItIsPlaced // Contoh menyembunyikan objek sampai ditempatkan
            };

            // Membuat instance prefab di lokasi yang ditentukan
            var instance = PlaceAtLocation.CreatePlacedInstance(prefab, location, placeOptions);

            // Tambahkan instance ke dalam list atau array jika diperlukan
            // _placeAtComponents.Add(instance.GetComponent<PlaceAtLocation>());
            _placeAtComponents.Add(instance.GetComponent<PlaceAtLocation>());
        }

        private Texture2D _separatorTexture;
        private Texture2D separatorTexture
        {
            get
            {
                if (_separatorTexture == null)
                {
                    _separatorTexture = new Texture2D(1, 1);
                    _separatorTexture.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f));
                    _separatorTexture.Apply();
                }

                return _separatorTexture;
            }
        }

        public void StartRoute(Location dest)
        {
            s.destination = dest;

            if (ARLocationProvider.Instance.IsEnabled)
            {
                loadRoute(ARLocationProvider.Instance.CurrentLocation.ToLocation());
            }
            else
            {
                ARLocationProvider.Instance.OnEnabled.AddListener(loadRoute);
            }
        }

        public void EndRoute()
        {
            ARLocationProvider.Instance.OnEnabled.RemoveListener(loadRoute);
            ARSession.SetActive(false);
            ARSessionOrigin.SetActive(false);
            RouteContainer.SetActive(false);
            Camera.gameObject.SetActive(true);
            s.View = View.SearchMenu;
        }

        private void loadRoute(Location _)
        {
            if (s.destination != null)
            {
                var api = new MapboxApi(MapboxToken);
                var loader = new RouteLoader(api);
                StartCoroutine(
                        loader.LoadRoute(
                            new RouteWaypoint { Type = RouteWaypointType.UserLocation },
                            new RouteWaypoint { Type = RouteWaypointType.Location, Location = s.destination },
                            (err, res) =>
                            {
                                if (err != null)
                                {
                                    s.ErrorMessage = err;
                                    s.Results = new List<GeocodingFeature>();
                                    return;
                                }

                                ARSession.SetActive(true);
                                ARSessionOrigin.SetActive(true);
                                RouteContainer.SetActive(true);
                                Camera.gameObject.SetActive(false);
                                s.View = View.Route;

                                currentPathRenderer.enabled = true;
                                MapboxRoute.RoutePathRenderer = currentPathRenderer;
                                MapboxRoute.BuildRoute(res);
                                currentResponse = res;
                                buildMinimapRoute(res);
                            }));
            }
        }

        private GameObject minimapRouteGo;
        private RouteResponse currentResponse;

        private void buildMinimapRoute(RouteResponse res)
        {
            var geo = res.routes[0].geometry;
            var vertices = new List<Vector3>();
            var indices = new List<int>();

            var worldPositions = new List<Vector2>();

            foreach (var p in geo.coordinates)
            {
                /* var pos = Mapbox.Unity.Utilities.Conversions.GeoToWorldPosition(
                        p.Latitude,
                        p.Longitude,
                        Map.CenterMercator,
                        Map.WorldRelativeScale
                        ); */

                // Mapbox.Unity.Utilities.Conversions.GeoToWorldPosition
                var pos = Map.GeoToWorldPosition(new Mapbox.Utils.Vector2d(p.Latitude, p.Longitude), true);
                worldPositions.Add(new Vector2(pos.x, pos.z));
                // worldPositions.Add(new Vector2((float)pos.x, (float)pos.y));
            }

            if (minimapRouteGo != null)
            {
                minimapRouteGo.Destroy();
            }

            minimapRouteGo = new GameObject("minimap route game object");
            minimapRouteGo.layer = MinimapLayer;

            var mesh = minimapRouteGo.AddComponent<MeshFilter>().mesh;

            var lineWidth = BaseLineWidth * Mathf.Pow(2.0f, Map.Zoom - 18);
            LineBuilder.BuildLineMesh(worldPositions, mesh, lineWidth);

            var meshRenderer = minimapRouteGo.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MinimapLineMaterial;
        }

        IEnumerator search()
        {
            var api = new MapboxApi(MapboxToken);

            yield return api.QueryLocal(s.QueryText, true);

            if (api.ErrorMessage != null)
            {
                s.ErrorMessage = api.ErrorMessage;
                s.Results = new List<GeocodingFeature>();
            }
            else
            {
                s.Results = api.QueryLocalResult.features;
            }
        }

        Vector3 lastCameraPos;
        void Update()
        {
            if (s.View == View.Route)
            {
                var cameraPos = Camera.main.transform.position;

                var arLocationRootAngle = ARLocationManager.Instance.gameObject.transform.localEulerAngles.y;
                var cameraAngle = Camera.main.transform.localEulerAngles.y;
                var mapAngle = cameraAngle - arLocationRootAngle;

                MapboxMapCamera.transform.eulerAngles = new Vector3(90, mapAngle, 0);

                if ((cameraPos - lastCameraPos).magnitude < MinimapStepSize) {
                    return;
                }

                lastCameraPos = cameraPos;

                var location = ARLocationManager.Instance.GetLocationForWorldPosition(Camera.main.transform.position);

                Map.SetCenterLatitudeLongitude(new Mapbox.Utils.Vector2d(location.Latitude, location.Longitude));
                Map.UpdateMap();

            }
            else
            {
                MapboxMapCamera.transform.eulerAngles = new Vector3(90, 0, 0);
            }
        }
    }
}

// Define your Result class structure here
public class Result
{
    public string place_name { get; set; }
    // Add other properties as needed
}