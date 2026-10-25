using System;
using UnityEngine;

namespace Sokoban.Presentation
{
    [Serializable]
    public sealed class LevelConfig
    {
        [SerializeField] private string address;

        public string Address => address;

        public LevelConfig(string address) => 
            this.address = address;
    }
}
