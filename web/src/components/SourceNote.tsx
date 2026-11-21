interface SourceNoteProps {
  source: string;
  label?: string;
}

export default function SourceNote({ source, label = 'Source' }: SourceNoteProps) {
  return (
    <p className="source-note">
      <span>{label}:</span> {source}
    </p>
  );
}
