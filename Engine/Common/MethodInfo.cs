namespace JocysCom.VS.AiCompanion.Engine
{
	public class MethodInfo
	{

		public MethodInfo() { }

		public MethodInfo(System.Reflection.MethodInfo info)
		{
			Name = info.Name;
			DeclaringTypeName = info.DeclaringType.Name;
			DeclaringTypeNamespace = info.DeclaringType.Namespace;
		}

		public string Name { get; set; }
		public string DeclaringTypeName { get; set; }
		public string DeclaringTypeNamespace { get; set; }

	}
}
