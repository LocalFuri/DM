using System;

namespace DM.Items
{
  [Serializable]
  public class ItemInstance
  {
    public int ItemId;
    public int Quantity = 1;

    public ItemInstance(int itemId)
    {
      ItemId = itemId;
    }

    public ItemInstance(int itemId, int quantity)
    {
      ItemId = itemId;
      Quantity = quantity;
    }

    public ItemDefinition Definition
    {
      get
      {
        return ItemDatabase.GetById(ItemId);
      }
    }
  }
}