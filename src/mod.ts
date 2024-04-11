import { DependencyContainer } from "tsyringe";
import { Ilogger } from "@spt-aki/models/spt/utils/Ilogger";
import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ItemHelper } from "@spt-aki/helpers/ItemHelper";

class BUMARevamped implements IPostDBLoadMod
{
	private config = require("../config/config.json");

	public postDBLoad(container: DependencyContainer): void 
	{
		// Get the logger from the server container.
		const logger = container.resolve<Ilogger>("WinstonLogger");
		logger.info(`[BUMA-Revamped] Loading`);
		// Get database from server.
		const db = container.resolve<DatabaseServer>("DatabaseServer");	
		const itemHelper = container.resolve<ItemHelper>("ItemHelper");
		// Get tables from database
		const tables = db.getTables();    
		// Get item database from tables
		const botsDB = tables.bots.types;

		let error = false;
		let validAmmo = {};
		for (const caliber in this.config.ammoToUse) {
			const ammoIds = this.config.ammoToUse[caliber];
			let validAmmoIds = {};
		
			for (const ammoId in ammoIds) {
				if (itemHelper.isItemInDb(ammoId)) {
					// If the ammo ID exists, add it to the validAmmoIds
					validAmmoIds[ammoId] = ammoIds[ammoId];
					if (this.config.debug) {
						logger.info(`[BUMA-Revamped] Ammo ${ammoId} found in the database`);
					}
				} else if (!this.config.disableWarnings) {
					logger.error(`[BUMA-Revamped] Ammo ${ammoId} not found in the database, deleting it from the list of ammo (aka it won't appear on bots).`);
					error = true;
				}
			}
		
			if (Object.keys(validAmmoIds).length > 0) {
				// If validAmmoIds is not empty, add it to validAmmo
				validAmmo[caliber] = validAmmoIds;
			} else {
				logger.info(`[BUMA-Revamped] Caliber ${caliber} is empty, deleting it from the list of ammo (aka not changing this caliber on any of the bots).`);
			}
		}
		if (error) {
			logger.error(`\n\n[BUMA-Revamped] If mod complains that ammo was not found in database but you sure it's in there it can be because this mod loads before the one that adds ammo. Make sure this mod loads after mods that add ammo. Mods loaded in alphabetical order of the mod folder names. So edit mod folder name to be after the mod that adds ammo\n\n`);
		}
		
		this.config.botsToReplaceAmmoFor.forEach((botName: string) => {
			if (botsDB[botName] && botsDB[botName].inventory) {
				const botAmmo = botsDB[botName].inventory.Ammo;
				// Iterate through the ammo types in the configuration
				for (const caliber in validAmmo) {
					if (botAmmo[caliber]) {
						// Replace the bot's ammo with config specified ammo
						botAmmo[caliber] = validAmmo[caliber];
					}
				}
			} else {
				logger.error(`[BUMA-Revamped] Bot ${botName} not found in the database or missing inventory.`);
			}
		});
		logger.info(`[BUMA-Revamped] Finished Loading`);
	}
}

module.exports = { mod: new BUMARevamped() }