using UnityEngine;

namespace Sokoban.Unity
{
    [CreateAssetMenu(menuName = "Sokoban/View Catalog")]
    public sealed class ViewCatalog : ScriptableObject
    {
        [SerializeField] private GameObject _floorPrefab;
        [SerializeField] private GameObject _wallPrefab;
        [SerializeField] private GameObject _boxPrefab;
        [SerializeField] private Material _goalMaterial;
        [SerializeField] private Material _playerMaterial;

        public GameObject FloorPrefab => _floorPrefab;
        public GameObject WallPrefab => _wallPrefab;
        public GameObject BoxPrefab => _boxPrefab;
        public Material GoalMaterial => _goalMaterial;
        public Material PlayerMaterial => _playerMaterial;
    }
}
