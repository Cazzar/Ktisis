using System.Collections.Generic;
using System.Linq;

using Dalamud.Plugin.Services;
using Dalamud.Utility;

using Ktisis.Core.Attributes;
using Ktisis.GameData.Excel;

namespace Ktisis.Services.Data;

public interface INameResolver {
	public string? GetWeaponName(ushort id, ushort secondId, ushort variant);
}

[Singleton]
public class NamingService : INameResolver {
	private readonly IDataManager _data;
	
	public NamingService(
		IDataManager data
	) {
		this._data = data;
	}
	
	// Weapons

	public string? GetWeaponName(ushort id, ushort secondId, ushort variant) {
		if (id == 0) return null;

		var weapon = this.GetWeapons().FirstOrDefault(wep => {
			if (wep.Model.Matches(id, secondId, variant))
				return true;
			return wep.SubModel.Id != 0 && wep.SubModel.Matches(id, secondId, variant);
		});

		return !weapon.Name.IsNullOrEmpty() ? weapon.Name : null;
	}
	
	private IEnumerable<ItemSheet> GetWeapons() => this._data
		.GetExcelSheet<ItemSheet>()
		.Where(item => item.IsWeapon());
}
