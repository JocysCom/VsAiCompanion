namespace JocysCom.VS.AiCompanion.Engine.Speech
{
	public class BlendShape
	{
		public int FrameIndex { get; set; }

		public int Offset => (int)(FrameIndex * 1000M / 60M);

		public decimal[][] BlendShapes { get; set; }
	}
}
