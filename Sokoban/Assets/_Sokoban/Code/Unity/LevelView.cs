using System;
using DefaultEcs;
using UnityEngine;
using System.Linq;
using Sokoban.Core;
using Sokoban.Unity.Ecs;
using Sokoban.Unity.Ecs.Model;
using Sokoban.Core.Components;
using System.Collections.Generic;

namespace Sokoban.Unity
{
    public sealed class LevelView : IDisposable
    {
        private readonly Transform _root;

        internal LevelView(Transform parent)
        {
            _root = new GameObject("Level view").transform;
            _root.SetParent(parent, false);
        }

        internal void Build(LevelDefinition level, World world, ViewCatalog catalog)
        {
            Dictionary<GridPosition, Transform> goalViews = CreateFloors(level, catalog.FloorPrefab, catalog.GoalMaterial);
            CreateEntityViews(world, goalViews, catalog.WallPrefab, catalog.BoxPrefab, catalog.PlayerMaterial);
        }

        public void Dispose()
        {
            if (_root == null)
            {
                return;
            }
            
            _root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(_root.gameObject);
        }

        private Dictionary<GridPosition, Transform> CreateFloors(LevelDefinition level, GameObject floorPrefab, Material goalMaterial)
        {
            var goalViews = new Dictionary<GridPosition, Transform>();
            
            for (var y = 0; y < level.Height; y++)
            {
                for (var x = 0; x < level.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    bool isGoal = level.Goals.Contains(position);
                    
                    Transform floor = InstantiateView(floorPrefab, position, isGoal 
                        ? "Goal"
                        : "Floor");
                    
                    floor.position = GridCoordinates.ToWorld(position);

                    if (isGoal == false)
                    {
                        continue;
                    }
                    
                    floor.GetComponentInChildren<Renderer>().sharedMaterial = goalMaterial;
                    goalViews.Add(position, floor);
                }
            }

            return goalViews;
        }

        private void CreateEntityViews(
            World world, 
            Dictionary<GridPosition, 
            Transform> goalViews, 
            GameObject wallPrefab, 
            GameObject boxPrefab, 
            Material playerMaterial)
        {
            foreach (Entity entity in world)
            {
                var position = entity.Get<GridPosition>();
                
                Transform view = entity switch
                {
                    _ when entity.Has<WallTag>() => InstantiateView(wallPrefab, position, "Wall"),
                    _ when entity.Has<BoxTag>() => InstantiateView(boxPrefab, position, "Box"),
                    _ when entity.Has<PlayerTag>() => CreatePlayerView(wallPrefab, position, playerMaterial),
                    _ when entity.Has<GoalTag>() => goalViews[position],
                    _ => null
                };

                if (view != null)
                {
                    BindView(entity, view);
                }
            }
        }

        private Transform CreatePlayerView(GameObject prefab, GridPosition position, Material material)
        {
            Transform player = InstantiateView(prefab, position, "Player");
            player.localScale = new Vector3(0.55f, 1f, 0.55f);
            player.GetComponentInChildren<Renderer>().sharedMaterial = material;
            return player;
        }

        private void BindView(Entity entity, Transform target)
        {
            var view = target.GetComponent<EntityView>();
            
            if (view == null)
            {
                throw new InvalidOperationException("Assign an EntityView on each grid prefab root.");
            }

            view.Install(entity);
        }

        private Transform InstantiateView(GameObject prefab, GridPosition position, string label)
        {
            Transform view = UnityEngine.Object.Instantiate(prefab, _root, false).transform;
            
            view.name = $"{label} ({position.X}, {position.Y})";
            view.rotation = prefab.transform.rotation;
            view.position = GridCoordinates.ToWorld(position);
            
            return view;
        }
    }
}
