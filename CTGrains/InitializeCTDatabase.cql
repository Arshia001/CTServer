CREATE TABLE ct_games (
	id int,
	sort float,
	name text,
	board_id ascii, // resource ID for the board which is used for this game
	entrance_fee bigint,
	reward bigint,
	is_active boolean,
	min_level int,
	xp int,
	can_watch_video_ad boolean,
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_spin_rewards (
	id int,
	name text,
	reward_type ascii, // The type of reward, e.g. gold, gem, etc.
	count int,
	chance float,
	is_active boolean,
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_spin_multipliers (
	id int,
	multiplier int,
	chance float,
	is_active boolean,
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_pack_categories (
	id int,
	sort float,
	name text,
	purchase_deadline timestamp, // Each offer category will at most have one of these limits set
	purchase_duration_minutes int,
	is_special boolean,
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_packs (
	id int,
	sort float,
	name text,
	category int,
	tag text, // best-selling, best value, etc.
	image_id ascii,
	price_currency ascii,
	iab_sku ascii,
	price int,
	currency_contents map<ascii, int>, // type of currency -> number contained
	item_contents set<int>, // IDs of customization items
	value_specifier text, // Text to indicate value for special packs, e.g. '4X', '25% Off'
	purchase_count_limit int,
	is_active boolean,
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_customization_items (
	id int,
	sort float,
	resource_id ascii,
	category ascii, // dice, cups, checkers, profile details (hat, eye, mouth, etc.)
	price_currency ascii,
	price int,
	is_purchasable boolean, // either this is a special reward handed out with packs only, or it's just not on sale anymore
	// an is_active column makes no sense here since items already purchased by players MUST remain in their inventory
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_levels (
	level int,
	xp int,
	reward int,
	reward_currency ascii,
	PRIMARY KEY(level)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE TABLE ct_client_statistics
(
	id uuid,
	client_id uuid,
	client_type tinyint,
	last_connection_time timestamp,
	
	PRIMARY KEY (id)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

CREATE INDEX ix_client_statistics_type ON ct_client_statistics(client_type);

CREATE TABLE ct_config (
	key ascii,
	value text,
	PRIMARY KEY (key)
) WITH compression = {
	'class' : 'LZ4Compressor',
	'enabled' : true
};

INSERT INTO ct_config (key,value) VALUES ('BkgmTurnTimeSeconds','30');
INSERT INTO ct_config (key,value) VALUES ('BkgmPenalizedTurnTimeSeconds','15');
INSERT INTO ct_config (key,value) VALUES ('BkgmExtraWaitTimePerTurnSeconds','3');
INSERT INTO ct_config (key,value) VALUES ('BkgmExtraTimePerDiceRollSeconds','5');
INSERT INTO ct_config (key,value) VALUES ('BkgmNumInactiveTurnsToLose','3');
INSERT INTO ct_config (key,value) VALUES ('BkgmAIInitPlayTimeSeconds','4');
INSERT INTO ct_config (key,value) VALUES ('BkgmAITimeBetweenPlaysSeconds','1');
INSERT INTO ct_config (key,value) VALUES ('BkgmAIPlayTimeVariation','0.8');
INSERT INTO ct_config (key,value) VALUES ('LeaderBoardSaveIntervalSeconds','60');
INSERT INTO ct_config (key,value) VALUES ('LeaderBoardNumTopScoresToReturn','20');
INSERT INTO ct_config (key,value) VALUES ('LeaderBoardNumAroundScoresToReturn','5');
INSERT INTO ct_config (key,value) VALUES ('MatchMakingInitWindowSize','3');
INSERT INTO ct_config (key,value) VALUES ('MatchMakingWindowIncrement','1');
INSERT INTO ct_config (key,value) VALUES ('MatchMakingIntervalSeconds','5');
INSERT INTO ct_config (key,value) VALUES ('MatchMakingNumUnsuccessfulAttemptsToMatchAI','4');
INSERT INTO ct_config (key,value) VALUES ('SpinnerIntervalMinutes','0');
INSERT INTO ct_config (key,value) VALUES ('BazaarClientID','');
INSERT INTO ct_config (key,value) VALUES ('BazaarClientSecret','');
INSERT INTO ct_config (key,value) VALUES ('BazaarRefreshCode','');
INSERT INTO ct_config (key,value) VALUES ('ClientLatestVersion','0');
INSERT INTO ct_config (key,value) VALUES ('ClientEarliestSupportedVersion','0');
INSERT INTO ct_config (key,value) VALUES ('UserInitialInventory','0,100,201');
INSERT INTO ct_config (key,value) VALUES ('UserInitialActiveItems','0,100,201');
INSERT INTO ct_config (key,value) VALUES ('UserInitialGold','100000');
INSERT INTO ct_config (key,value) VALUES ('UserInitialGems','100000');
INSERT INTO ct_config (key,value) VALUES ('IsMultiplayerAllowed','true');
INSERT INTO ct_config (key,value) VALUES ('IsServerUnderMaintenance','false');
INSERT INTO ct_config (key,value) VALUES ('VideoAdReward','50');
INSERT INTO ct_config (key,value) VALUES ('MaximumNameLength','16');
INSERT INTO ct_config (key,value) VALUES ('NumCheckersToHitPerDayForReward','30');
INSERT INTO ct_config (key,value) VALUES ('CheckerHitRewardPerDay','50');
