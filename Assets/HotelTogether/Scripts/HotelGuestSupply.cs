using UnityEngine;
namespace HotelTogether
{
    public enum GuestNeed { None, CheckIn, Meal, Water, Towel, CheckOut, Help, Shelter, Rescue, Luggage }
    public enum MealDish { ChickenRice, VegetableRice }
    public sealed class HotelGuestSupply : MonoBehaviour
    {
        public GuestNeed type;
        public MealDish dish;
        public static string DishName(MealDish dish) => dish == MealDish.VegetableRice ? "Vegetable rice" : "Chicken rice";
        public string Label => type == GuestNeed.Meal ? DishName(dish) : type == GuestNeed.Water ? "Water" : "Clean towel";
    }
}
