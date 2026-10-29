export type CompCountFilter = {
  id: number;
  type: 'role' | 'spec';
  key: string;
  op: string;
  count: number;
};
