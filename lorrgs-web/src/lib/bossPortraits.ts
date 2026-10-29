const journalBosses = new Set(
  `
lucifron magmadar gehennas garr baron-geddon shazzrah sulfuron-harbinger
golemagg-the-incinerator majordomo-executus ragnaros onyxia razorgore-the-untamed
vaelastrasz-the-corrupt broodlord-lashlayer firemaw ebonroc flamegor chromaggus
nefarian high-priest-venoxis bloodlord-mandokir kurinnaxx general-rajaxx moam
buru-the-gorger ayamiss-the-hunter ossirian-the-unscarred the-prophet-skeram
silithid-royalty battleguard-sartura fankriss-the-unyielding viscidus princess-huhuran
twin-emperors ouro cthun anubrekhan grand-widow-faerlina maexxna noth-the-plaguebringer
heigan-the-unclean loatheb instructor-razuvious gothik-the-harvester patchwerk grobbulus
gluth thaddius sapphiron kelthuzad ghamoo-ra gelihast grubbis viscous-fallout
electrocutioner-6000 akumai mekgineer-thermaplugg crowd-pummeler-9-60 shade-of-eranikus
lorgus-jett avatar-of-hakkar baron-aquanis lady-sarevess attumen-the-huntsman moroes
maiden-of-virtue the-curator terestian-illhoof shade-of-aran netherspite prince-malchezaar
nightbane high-king-maulgar gruul-the-dragonkiller magtheridon hydross-the-unstable
the-lurker-below leotheras-the-blind fathom-lord-karathress morogrim-tidewalker lady-vashj
alar void-reaver high-astromancer-solarian kaelthas-sunstrider rage-winterchill anetheron
kazrogal azgalor archimonde high-warlord-najentus supremus shade-of-akama teron-gorefiend
mother-shahraz reliquary-of-souls illidan-stormrage nalorakk janalai halazzi akilzon
hex-lord-malacrass daakara brutallus kalecgos felmyst eredar-twins muru kiljaeden ulatek
eranog terros magmorax rashok-the-elder assault-of-the-zaqali magmaw argaloth alizabal
occuthar maloriak halfus-wyrmbreaker chimaeron alakir sinestra chogall lord-rhyolith
alysrazor shannox baleroc morchok yorsahj-the-unsleeping ultraxion warlord-zonozz
feng-the-accursed garajal-the-spiritbinder elegon imperial-vizier-zorlok blade-lord-tayak
garalon wind-lord-meljarak amber-shaper-unsok atramedes grand-empress-shekzeer tsulong
lei-shi sha-of-fear horridon ji-kun dark-animus tortos megaera primordius lei-shen ra-den
council-of-elders immerseus norushen iron-qon sha-of-pride galakras iron-juggernaut
general-nazgrim korkron-dark-shaman malkorok siegecrafter-blackfuse garrosh-hellscream
jinrokh-the-breaker thok-the-bloodthirsty
`
    .trim()
    .split(/\s+/)
);

const journalAliases: Record<string, string> = {
  'gurtogg-bloodboil': 'gurtoggbloodboil',
  'the-illidari-council': 'illidari-council',
  'amalgamation-chamber': 'the-amalgamation-chamber',
  'forgotten-experiments': 'the-forgotten-experiments',
  'sennarth-the-cold-breath': 'sennarththecoldbreath',
  'dathea-ascended': 'datheaascended',
  'raszageth-the-storm-eater': 'raszageththestorm-eater',
  'warmaster-blackhorn': 'warmasterblackhorn'
};

export function getBossPortraitUrl(slug: string): string {
  const filename = journalAliases[slug] ?? (journalBosses.has(slug) ? slug : '');
  return filename
    ? `https://wow.zamimg.com/images/wow/journal/ui-ej-boss-${filename}.png`
    : 'https://wow.zamimg.com/images/icons/boss.gif';
}
