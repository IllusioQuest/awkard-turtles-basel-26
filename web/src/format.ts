export function formatNumber(value: number, digits = 0): string {
  return new Intl.NumberFormat('en-CH', {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format(value);
}

export function formatPercent(share: number): string {
  return `${formatNumber(share * 100, 1)} %`;
}

export function formatChf(value: number, digits = 0): string {
  return `CHF ${formatNumber(value, digits)}`;
}
