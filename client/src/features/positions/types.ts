export type Position = {
  source: string
  symbol: string
  name?: string
  assetType: 'cash' | 'security' | 'crypto' | 'other'
  quantity?: number
  displayValue?: number
  connectionLabel: string
  asOf: string
}
