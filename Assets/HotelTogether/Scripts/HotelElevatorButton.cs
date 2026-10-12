using UnityEngine;
namespace HotelTogether
{
 public sealed class HotelElevatorButton : MonoBehaviour
 {
  public HotelElevator elevator;
  public int floor;
  public bool inside;
  public int doorCommand,callDirection;
  public string ActionLabel=>doorCommand==1?"OPEN DOORS":doorCommand==2?"CLOSE DOORS":inside?"SELECT "+elevator.floors[floor].label:callDirection>0?"CALL UP":"CALL DOWN";
  public bool Use(HotelPlayer player)=>doorCommand!=0?elevator.ControlDoors(player,doorCommand==1):inside?elevator.SelectFloor(player,floor):elevator.Call(player,floor,callDirection);
 }
}
