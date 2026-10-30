export const reportMetadata = (code = 'ReportA') => ({
  reportData: {
    report: {
      code,
      title: `Raid ${code}`,
      startTime: 1700000000000,
      zone: { id: 1, name: "Zul'Aman" },
      fights: [
        {
          id: 1,
          name: "Zul'jin",
          encounterID: 10,
          kill: true,
          difficulty: 3,
          startTime: 1000,
          endTime: 101000
        },
        {
          id: 2,
          name: "Zul'jin",
          encounterID: 10,
          kill: false,
          difficulty: 3,
          startTime: 110000,
          endTime: 150000
        },
        {
          id: 3,
          name: 'Nalorakk',
          encounterID: 11,
          kill: true,
          difficulty: 3,
          startTime: 160000,
          endTime: 240000
        },
        {
          id: 4,
          name: 'Trash',
          encounterID: 0,
          kill: null,
          difficulty: null,
          startTime: 0,
          endTime: 500
        }
      ]
    }
  }
});
