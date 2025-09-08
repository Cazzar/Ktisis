using System;

namespace Ktisis.Actions.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ActionAttribute : Attribute {
	public readonly string Name;

	public ActionAttribute(
		string name
	) {
		this.Name = name;
	}
}
