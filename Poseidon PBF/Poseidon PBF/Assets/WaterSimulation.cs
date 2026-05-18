using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;


/*
Helper Grid class to build a neighbour search grid
*/
public class NeighbourGrid
{

  public NeighbourGrid(List<Transform> tfs, float cellSize, int cellCount)
  {

    transforms = tfs;
    w = cellSize;
    N = cellCount;

    cells = new List<List<int>>(N);

    for (int i = 0; i < N; i++)
    {
      cells.Add(new List<int>());
    }

    for (int i = 0; i < transforms.Count; i++)
    {
      int h = (int)hash(transforms[i].position);
      cells[h].Add(i);
    }
  }


  private float w;
  private int N;

  private List<List<int>> cells;
  private List<Transform> transforms;

  const uint prime1 = 73856093;
  const uint prime2 = 19349663;


  (int, int) cell(Vector2 pos)
  {
    int x = Mathf.FloorToInt(pos.x / w);
    int y = Mathf.FloorToInt(pos.y / w);
    return (x, y);
  }

  uint hash(Vector2 pos)
  {
    // Calculate grid cell
    (int, int) t = cell(pos);

    // Cast to unsigned ints and multiply by primes
    uint ux = (uint)t.Item1 * prime1;
    uint uy = (uint)t.Item2 * prime2;

    // XOR the values together, and modulo by N, the number of cells
    return (ux ^ uy) % (uint)N;
  }

  uint hash((int, int) t)
  {
    // Cast to unsigned ints and multiply by primes
    uint ux = (uint)t.Item1 * prime1;
    uint uy = (uint)t.Item2 * prime2;

    // XOR the values together, and modulo by N, the number of cells
    return (ux ^ uy) % (uint)N;
  }

  public List<int> neighbours(Vector2 pos, float d)
  {
    List<int> n = new List<int>();

    // We need to calculate bounds on the cells we need to explore.
    // We calcuate rough bounds as we suppose w ~ 2h where h is the half kernel radius.
    // Thus, in most cases, hopefully, a square suffices
    int nc = Mathf.CeilToInt(d / w);
    (int, int) c = cell(pos);
    (int, int) min = (c.Item1 - nc, c.Item2 - nc);
    (int, int) max = (c.Item1 + nc, c.Item2 + nc);
    for (int i = min.Item1; i <= max.Item1; i++)
      for (int j = min.Item2; j <= max.Item2; j++)
        foreach (int cid in cells[(int)hash((i, j))])
          if ((pos - (Vector2)transforms[cid].position).magnitude <= d)
            n.Add(cid);
    return n;


  }
}




public class WaterSimulation : MonoBehaviour
{
  [Header("Simulation Settings")]
  public GameObject waterParticlePrefab;
  public int particleCount = 200;
  public float convergenceSpeed = 10f;

  [Header("Display and Demo settings")]
  public TMP_Text densityLabel;
  public Color activeColor = new Color(1f, 0f, 0f);
  public Color inactiveColor = new Color(1f, 1f, 1f);
  public float smoothingRadius = 2f;
  public float lineWidth = 0.05f;
  public int nsegments = 50;


  private float minX, maxX, minY, maxY;

  private List<Transform> particles = new List<Transform>();
  private List<SpriteRenderer> renderers = new List<SpriteRenderer>();

  private NeighbourGrid grid;

  InputAction mousePosition;
  LineRenderer lines;
  LineRenderer circle;

  void DrawCircle(Vector2 o)
  {
    Vector3[] points = new Vector3[nsegments + 1];

    for (int i = 0; i < nsegments; i++)
    {
      float angle = ((float)i) / nsegments * 2 * Mathf.PI;
      float x = smoothingRadius * Mathf.Cos(angle) + o.x;
      float y = smoothingRadius * Mathf.Sin(angle) + o.y;
      points[i] = new Vector3(x, y, 0);
    }

    points[nsegments] = new Vector2(smoothingRadius + o.x, o.y);

    circle.positionCount = points.Length;
    circle.SetPositions(points);
    Debug.Log("Circle drawn");
  }


  void Start()
  {

    Camera cam = Camera.main;

    mousePosition = InputSystem.actions.FindAction("Point");

    // An object cannot have 2 renderers (therefore it cannot have two line renderers), so we must create other containing objects

    GameObject linesObject = new GameObject("Lines");
    linesObject.transform.SetParent(this.transform);

    lines = linesObject.AddComponent<LineRenderer>();
    lines.material = new Material(Shader.Find("Sprites/Default"));
    lines.startWidth = lineWidth;
    lines.endWidth = lineWidth;


    GameObject circleObject = new GameObject("Circle");
    circleObject.transform.SetParent(this.transform);

    circle = circleObject.AddComponent<LineRenderer>();
    circle.material = new Material(Shader.Find("Sprites/Default"));
    circle.startWidth = lineWidth;
    circle.endWidth = lineWidth;


    DrawCircle(new Vector2(0, 0));


    float screenHeight = 2f * cam.orthographicSize;
    float screenWidth = screenHeight * cam.aspect;

    minX = cam.transform.position.x - (screenWidth / 2f);
    maxX = cam.transform.position.x + (screenWidth / 2f);
    minY = cam.transform.position.y - (screenHeight / 2f);
    maxY = cam.transform.position.y + (screenHeight / 2f);

    for (int i = 0; i < particleCount; i++)
    {
      // We spawn the particle randomly inside the box
      Vector2 spawn = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
      // We create a new instance of particle at given position
      GameObject newParticle = Instantiate(waterParticlePrefab, spawn, Quaternion.identity);
      // We take a reference to the object's transform, allowing us to move it later
      particles.Add(newParticle.transform);
      renderers.Add(newParticle.GetComponent<SpriteRenderer>());
    }

    // We build a Neighbour search grid
    float w = 1000 / particleCount;
    grid = new NeighbourGrid(particles, w, Mathf.FloorToInt((maxX - minX) * (maxY - minY) / (w * w)));

  }


  void Update()
  {

    Vector2 pos = mousePosition.ReadValue<Vector2>();

    Vector2 worldMousePos = Camera.main.ScreenToWorldPoint(pos);

    List<int> neighbours = grid.neighbours(worldMousePos, smoothingRadius);

    Vector3[] linePoints = new Vector3[neighbours.Count * 2];

    for (int i = 0; i < neighbours.Count; i++)
    {
      linePoints[2 * i] = particles[neighbours[i]].position;
      linePoints[2 * i + 1] = worldMousePos;
    }
    lines.positionCount = linePoints.Length;
    lines.SetPositions(linePoints);

    DrawCircle(worldMousePos);

    densityLabel.text = string.Format("Number of neighbours : {0,5:d}", neighbours.Count);

    for (int i = 0; i < particleCount; i++)
    {
      // Physics loop
      // For now we move the particle to the center of the screen slowly
      // float d = particles[i].position.magnitude;
      // particles[i].position *= 1 - (d * Time.deltaTime * convergenceSpeed / ((maxX - minX) * 100f));


      // We color according to the distance to pointed point
      if (neighbours.Contains(i))
      {
        renderers[i].color = activeColor;
        // Debug.DrawLine(worldMousePos, particles[i].position, activeColor, 0.0f, false);
      }
      else
        renderers[i].color = inactiveColor;


    }


  }

}
