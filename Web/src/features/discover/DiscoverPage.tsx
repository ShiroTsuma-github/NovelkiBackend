import { useQuery, useQueryClient } from '@tanstack/react-query'
import { BookPlus, Compass, Search, Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { api } from '@/api/client'
import { HttpError } from '@/api/http'
import type { BookCoverDto, PublicBookMetadataDto, PublicBookSnapshotDto } from '@/api/types'
import { Badge, buttonVariants, PageHeader, Surface } from '@/components/app/DesignSystem'
import { BookCoverArtwork } from '@/features/books/BookCoverSection'
import { DescribedMetadataPills, MetadataSummary } from '@/features/books/MetadataBadges'

export function DiscoverPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [copyingId, setCopyingId] = useState<string | null>(null)

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search.trim()), 200)
    return () => window.clearTimeout(timeout)
  }, [search])

  const books = useQuery({
    queryKey: ['public-books', debouncedSearch],
    queryFn: () => api.searchPublicBooks({ search: debouncedSearch || undefined, skip: 0, take: 40 }),
  })

  async function copyBook(snapshot: PublicBookSnapshotDto) {
    setCopyingId(snapshot.id)
    try {
      const result = await api.copyPublicBook(snapshot.id)
      await queryClient.invalidateQueries({ queryKey: ['books'] })
      toast.success(`“${snapshot.primaryTitle}” was added to your library.`)
      navigate(`/books/${result.bookId}`)
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setCopyingId(null)
    }
  }

  return (
    <div className="discover-page">
      <PageHeader
        description="Search snapshots shared by other readers. Adding one creates an independent copy that you can edit freely."
        eyebrow="Community shelf"
        title="Discover books"
      />

      <Surface className="discover-toolbar" tone="elevated">
        <label className="manage-search discover-search">
          <Search aria-hidden="true" className="h-4 w-4" />
          <span className="sr-only">Search shared books</span>
          <input
            autoComplete="off"
            placeholder="Search by title or author…"
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          {books.isFetching ? <span className="manage-search__status">Searching…</span> : null}
        </label>
        <div className="discover-toolbar__meta">
          <Compass className="h-4 w-4" />
          <span>{books.data?.total ?? 0} shared snapshots</span>
        </div>
      </Surface>

      {books.isError ? (
        <Surface className="discover-state" tone="danger">Could not load shared books. Try again.</Surface>
      ) : books.isPending ? (
        <Surface className="discover-state">Loading the community shelf…</Surface>
      ) : books.data.data.length === 0 ? (
        <Surface className="discover-state">
          <Sparkles className="mx-auto h-6 w-6" />
          <strong>No shared books found</strong>
          <span>{search.trim() ? 'Try a broader title or author search.' : 'Shared books will appear here when readers list them.'}</span>
        </Surface>
      ) : (
        <div className="discover-grid">
          {books.data.data.map((snapshot) => (
            <PublicBookCard
              copying={copyingId === snapshot.id}
              key={snapshot.id}
              snapshot={snapshot}
              onCopy={() => copyBook(snapshot)}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function PublicBookCard({ snapshot, copying, onCopy }: {
  snapshot: PublicBookSnapshotDto
  copying: boolean
  onCopy: () => void
}) {
  const cover: BookCoverDto | null = snapshot.coverUrl ? {
    id: snapshot.id,
    status: 'Found',
    imageUrl: snapshot.coverUrl,
    thumbnailImageUrl: snapshot.coverUrl,
    lastAttemptAt: snapshot.snapshotAt,
  } : null

  return (
    <Surface as="article" className="discover-card">
      <div className="discover-card__cover">
        <BookCoverArtwork
          className="rounded-none border-0"
          cover={cover}
          emptyLabel="Snapshot without a cover"
          preferredVariant="thumbnail"
          title={`Cover of ${snapshot.primaryTitle}`}
        />
      </div>
      <div className="discover-card__body">
        <div className="discover-card__heading">
          <div className="min-w-0">
            <MetadataSummary
              alternatives={snapshot.alternativeTitles}
              countNoun="alternative title"
              primary={snapshot.primaryTitle}
              primaryClassName="discover-card__title"
            />
            {snapshot.author ? (
              <MetadataSummary
                alternatives={snapshot.authorOtherNames}
                countNoun="alternative name"
                primary={snapshot.author}
                primaryClassName="discover-card__author"
              />
            ) : <span className="discover-card__author">Unknown author</span>}
          </div>
          <Badge tone="neutral">{snapshot.contentType}</Badge>
        </div>

        <p className="discover-card__description">{snapshot.description || 'No description was included in this snapshot.'}</p>

        <MetadataGroup label="Genres" values={snapshot.genres} />
        <MetadataGroup label="Tags" values={snapshot.tags} />

        <div className="discover-card__footer">
          <span>Snapshot {new Date(snapshot.snapshotAt).toLocaleDateString()}</span>
          <button
            className={buttonVariants.primary}
            disabled={copying || snapshot.isOwner}
            type="button"
            onClick={onCopy}
          >
            <BookPlus className="h-4 w-4" />
            {snapshot.isOwner ? 'Your listing' : copying ? 'Adding…' : 'Add to library'}
          </button>
        </div>
      </div>
    </Surface>
  )
}

function MetadataGroup({ label, values }: { label: string; values: PublicBookMetadataDto[] }) {
  if (values.length === 0) return null
  return (
    <div className="discover-card__metadata">
      <span>{label}</span>
      <DescribedMetadataPills
        descriptions={Object.fromEntries(values.map((item) => [item.name, item.description]))}
        maxVisible={4}
        values={values.map((item) => item.name)}
        variant="detail"
      />
    </div>
  )
}

function getErrorMessage(error: unknown) {
  if (error instanceof HttpError) return error.apiError.detail
  return error instanceof Error ? error.message : 'The book could not be added.'
}
