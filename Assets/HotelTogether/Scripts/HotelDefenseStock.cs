using UnityEngine;
namespace HotelTogether
{
    public sealed class HotelDefenseStock : MonoBehaviour
    {
        public DefenseStockType type;
        public void Collect(HotelCombat combat){if(type==DefenseStockType.Wood)combat.CollectWood();else combat.CollectMedicine();}
    }
}
