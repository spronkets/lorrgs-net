export type Season = {
  id: number;
  name: string;
  slug: string;
  raidIds: number[];
  startDate: string | null;
  endDate: string | null;
};
