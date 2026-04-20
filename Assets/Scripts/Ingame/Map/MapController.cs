using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace DeckRoguelike.UI
{
    /// <summary>
    /// 그리드 기반 가로 맵 컨트롤러
    /// - 좌→우 진행 방향
    /// - 상하좌우 4방향 이동
    /// - BFS로 보스 도달 가능성 검증 → 길 막힘 방지
    /// </summary>
    public class MapController : MonoBehaviour
    {
        [Header("=== Grid Settings ===")]
        [SerializeField] private int gridRows = 4;
        [SerializeField] private int gridCols = 15;
        [SerializeField] private float nodeSize = 150f;

        [Header("=== Node Distribution (나머지는 자동으로 Combat) ===")]
        [SerializeField] private int eliteCount   = 6;
        [SerializeField] private int eventCount   = 13;
        [SerializeField] private int restCount    = 7;
        [SerializeField] private int shopCount    = 3;
        [SerializeField] private int treasureCount = 4;

        [Header("=== UI References ===")]
        [SerializeField] private RectTransform mapContainer;
        [SerializeField] private Button closeMapButton;

        [Header("=== Prefabs ===")]
        [SerializeField] private GameObject mapNodePrefab;

        [Header("=== Player Marker ===")]
        [SerializeField] private RectTransform playerMarker;

        private MapNodeData[,] grid;
        private readonly List<GameObject> nodeObjects = new List<GameObject>();
        private Vector2Int currentGridPos = new Vector2Int(-1, -1);
        private int mapSeed;

        public event Action<NodeType, int> OnNodeSelected;

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(-1,  0), // 위
            new Vector2Int( 1,  0), // 아래
            new Vector2Int( 0, -1), // 왼쪽
            new Vector2Int( 0,  1), // 오른쪽
        };

        private void Start()
        {
            closeMapButton?.onClick.AddListener(CloseMap);
        }

        #region Map Generation

        public void GenerateNewMap(int seed = -1)
        {
            mapSeed = seed >= 0 ? seed : UnityEngine.Random.Range(0, 99999);
            UnityEngine.Random.InitState(mapSeed);
            Debug.Log($"[MapController] 맵 생성 (시드: {mapSeed})");

            ClearMap();
            GenerateGrid();
            Canvas.ForceUpdateCanvases(); // CreateVisuals 전에 확정해야 viewport 크기를 정확히 읽음
            CreateVisuals();
            SetStartNode();

        }

        private void ClearMap()
        {
            foreach (var obj in nodeObjects) Destroy(obj);
            nodeObjects.Clear();
            grid = null;
            currentGridPos = new Vector2Int(-1, -1);
        }

        private void GenerateGrid()
        {
            grid = new MapNodeData[gridRows, gridCols];

            // 전부 Combat으로 초기화
            for (int row = 0; row < gridRows; row++)
                for (int col = 0; col < gridCols; col++)
                    grid[row, col] = new MapNodeData { Type = NodeType.Combat, Row = row, Col = col };

            // 고정 위치: 시작(왼쪽 아래), 보스(오른쪽 위)
            grid[0, 0].Type                         = NodeType.Combat;
            grid[0, 0].IsStart                      = true;
            grid[gridRows - 1, gridCols - 1].Type   = NodeType.Boss;

            // 시작 노드와 보스를 제외한 자유 노드 목록
            var freeNodes = new List<Vector2Int>();
            for (int row = 0; row < gridRows; row++)
                for (int col = 0; col < gridCols; col++)
                    if (!(row == 0 && col == 0) && !(row == gridRows - 1 && col == gridCols - 1))
                        freeNodes.Add(new Vector2Int(row, col));

            // 시드 적용된 셔플
            for (int i = freeNodes.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (freeNodes[i], freeNodes[j]) = (freeNodes[j], freeNodes[i]);
            }

            int idx = 0;
            void Assign(NodeType type, int count)
            {
                for (int i = 0; i < count && idx < freeNodes.Count; i++, idx++)
                    grid[freeNodes[idx].x, freeNodes[idx].y].Type = type;
            }

            Assign(NodeType.Elite,    eliteCount);
            Assign(NodeType.Event,    eventCount);
            Assign(NodeType.Rest,     restCount);
            Assign(NodeType.Shop,     shopCount);
            Assign(NodeType.Treasure, treasureCount);
            // 나머지는 Combat (이미 초기화됨)
        }

        #endregion

        #region Visual Creation

        private void CreateVisuals()
        {
            if (mapNodePrefab == null || mapContainer == null) return;

            float totalWidth  = gridCols * nodeSize;
            float totalHeight = gridRows * nodeSize;

            mapContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            mapContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            mapContainer.anchoredPosition = Vector2.zero;

            float xStart = -(gridCols - 1) / 2f * nodeSize;
            float yStart = -(gridRows - 1) / 2f * nodeSize;

            for (int row = 0; row < gridRows; row++)
            {
                for (int col = 0; col < gridCols; col++)
                {
                    var nodeData = grid[row, col];
                    nodeData.Position = new Vector2(xStart + col * nodeSize, yStart + row * nodeSize);

                    GameObject nodeObj = Instantiate(mapNodePrefab, mapContainer);
                    RectTransform rt = nodeObj.GetComponent<RectTransform>();
                    rt.anchoredPosition = nodeData.Position;
                    rt.sizeDelta = new Vector2(nodeSize, nodeSize);

                    MapNodeUI nodeUI = nodeObj.GetComponent<MapNodeUI>();
                    if (nodeUI != null)
                    {
                        int r = row, c = col;
                        nodeUI.Initialize(nodeData);
                        nodeUI.OnClicked += () => HandleNodeClicked(r, c);
                    }

                    nodeData.NodeObject = nodeObj;
                    nodeObjects.Add(nodeObj);
                }
            }

        }

        private void SetStartNode()
        {
            // 시작 노드 (0,0) 자동 방문 처리
            var startNode = grid[0, 0];
            startNode.IsVisited = true;
            currentGridPos = new Vector2Int(0, 0);
            UpdateNodeVisual(startNode);

            // 플레이어 마커를 시작 노드 위치로 이동 후 맨 앞으로 올림
            if (playerMarker != null)
            {
                float offset = playerMarker.rect.height / 2f;
                playerMarker.anchoredPosition = startNode.Position + Vector2.up * offset;
                playerMarker.SetAsLastSibling();
            }

            // 인접 노드 접근 가능 처리
            foreach (var dir in Directions)
            {
                int nr = 0 + dir.x;
                int nc = 0 + dir.y;
                if (!IsInBounds(nr, nc)) continue;
                if (CanReachBoss(nr, nc))
                {
                    grid[nr, nc].IsAccessible = true;
                    UpdateNodeVisual(grid[nr, nc]);
                }
            }
        }

        #endregion

        #region Reachability (길 막힘 방지)

        /// <summary>
        /// (startRow, startCol)에서 출발해 미방문 노드만 통해
        /// 보스 열(gridCols-1)에 도달할 수 있는지 BFS로 검사.
        /// 이미 방문한 현재 노드는 제외하고 계산한다.
        /// </summary>
        private bool CanReachBoss(int startRow, int startCol)
        {
            bool[,] seen = new bool[gridRows, gridCols];
            seen[startRow, startCol] = true;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startRow, startCol));

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                if (pos.y == gridCols - 1) return true; // 보스 열 도달

                foreach (var dir in Directions)
                {
                    int nr = pos.x + dir.x;
                    int nc = pos.y + dir.y;
                    if (IsInBounds(nr, nc) && !seen[nr, nc] && !grid[nr, nc].IsVisited)
                    {
                        seen[nr, nc] = true;
                        queue.Enqueue(new Vector2Int(nr, nc));
                    }
                }
            }
            return false;
        }

        #endregion

        #region Node Interaction

        private void HandleNodeClicked(int row, int col)
        {
            var node = grid[row, col];
            if (!node.IsAccessible || node.IsVisited) return;

            Debug.Log($"[MapController] 노드 클릭: {node.Type} ({row},{col})");

            // 현재 노드 방문 처리
            node.IsVisited = true;
            currentGridPos = new Vector2Int(row, col);

            // 전체 접근 초기화
            ClearAllAccessibility();

            if (node.Type != NodeType.Boss) // 보스 노드가 아닌 경우에만 다음 이동 활성화
            {
                // 4방향 인접 노드 중 보스에 도달 가능한 노드만 접근 허용
                foreach (var dir in Directions)
                {
                    int nr = row + dir.x;
                    int nc = col + dir.y;

                    if (!IsInBounds(nr, nc))  continue;
                    if (grid[nr, nc].IsVisited) continue;

                    if (CanReachBoss(nr, nc))
                        grid[nr, nc].IsAccessible = true;
                }
            }

            UpdateAllNodeVisuals();

            // 플레이어 마커를 클릭한 노드로 이동
            // (CloseMap이 오브젝트를 비활성화하기 전에 즉시 위치 설정)
            if (playerMarker != null)
            {
                float offset = playerMarker.rect.height / 2f;
                playerMarker.anchoredPosition = node.Position + Vector2.up * offset;
                playerMarker.SetAsLastSibling();
            }

            InGameUIController.Instance?.CloseMap();
            OnNodeSelected?.Invoke(node.Type, node.Col);
        }

        private void ClearAllAccessibility()
        {
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    grid[r, c].IsAccessible = false;
        }

        #endregion

        #region Visual Update

        private void UpdateNodeVisual(MapNodeData node)
        {
            node.NodeObject?.GetComponent<MapNodeUI>()?.UpdateVisual();
        }

        private void UpdateAllNodeVisuals()
        {
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    UpdateNodeVisual(grid[r, c]);
        }

        #endregion

        #region Helpers

        private bool IsInBounds(int row, int col)
            => row >= 0 && row < gridRows && col >= 0 && col < gridCols;

        #endregion

        #region Public Methods

        public void ShowMap()
        {
            if (grid == null)
            {
                int seed = Core.GameManager.Instance?.RunSeed ?? -1;
                GenerateNewMap(seed);
            }
        }

        private void CloseMap()
        {
            InGameUIController.Instance?.CloseMap();
        }

        public int GetCurrentFloor() => currentGridPos.y >= 0 ? currentGridPos.y : 0;

        #endregion
    }

    #region Data Classes

    [System.Serializable]
    public class MapNodeData
    {
        public NodeType Type;
        public int Row;
        public int Col;
        public bool IsVisited;
        public bool IsAccessible;
        public bool IsStart;
        public Vector2 Position;

        [System.NonSerialized]
        public GameObject NodeObject;
    }

    public enum NodeType
    {
        Combat,
        Elite,
        Boss,
        Rest,
        Shop,
        Treasure,
        Event,
        Unknown
    }

    #endregion
}
