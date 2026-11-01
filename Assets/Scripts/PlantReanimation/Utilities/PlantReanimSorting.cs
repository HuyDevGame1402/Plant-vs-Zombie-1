
namespace PlantReanimation.Utilities
{
    public static class PlantReanimSorting
    {
        public static int GetOrder(int trackIndex)
        {
            // Track đứng trước trong JSON sẽ được render trước.
            return trackIndex;
        }
    }
}
