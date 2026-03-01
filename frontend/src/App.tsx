import { useState } from 'react'
import { Authenticator } from '@aws-amplify/ui-react'
import { fetchAuthSession } from 'aws-amplify/auth'

const API_URL = import.meta.env.VITE_API_URL

interface GdeltEvent {
    title: string
    url: string
    domain: string
    seendate: string
}

function AnalyzerApp() {
    const [topic, setTopic] = useState('')
    const [context, setContext] = useState('')
    const [analysis, setAnalysis] = useState('')
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState('')
    const [events, setEvents] = useState<GdeltEvent[]>([])
    const [loadingEvents, setLoadingEvents] = useState(false)

    const fetchEvents = async () => {
        setLoadingEvents(true)
        setError('')
        try {
            const response = await fetch(`${API_URL}events`)
            const data = await response.json()
            setEvents(data.events ?? [])
        } catch (err: any) {
            setError(err.message)
        } finally {
            setLoadingEvents(false)
        }
    }

    const selectEvent = (event: GdeltEvent) => {
        setTopic(event.title)
        setContext(`Source: ${event.domain} — ${event.url}`)
        setEvents([])
        setAnalysis('')
    }

    const analyze = async () => {
        if (!topic.trim()) return
        setLoading(true)
        setError('')
        setAnalysis('')

        try {
            const session = await fetchAuthSession()
            const token = session.tokens?.idToken?.toString()

            const response = await fetch(`${API_URL}analyze`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ topic, context })
            })

            if (!response.ok) throw new Error(`HTTP ${response.status}`)

            const data = await response.json()
            setAnalysis(data.analysis)
        } catch (err: any) {
            setError(err.message)
        } finally {
            setLoading(false)
        }
    }

    return (
        <div style={{ maxWidth: '800px', margin: '0 auto', padding: '2rem', fontFamily: 'system-ui' }}>
            <h1 style={{ borderBottom: '2px solid #333', paddingBottom: '0.5rem' }}>MotiveAI</h1>
            <p style={{ color: '#666', marginBottom: '2rem' }}>
                Follow the incentives. Ask not what happened — ask why.
            </p>

            <button
                onClick={fetchEvents}
                disabled={loadingEvents}
                style={{
                    marginBottom: '1.5rem',
                    padding: '0.5rem 1.25rem',
                    fontSize: '0.9rem',
                    backgroundColor: loadingEvents ? '#999' : '#444',
                    color: 'white',
                    border: 'none',
                    cursor: loadingEvents ? 'not-allowed' : 'pointer'
                }}
            >
                {loadingEvents ? 'Fetching...' : '⚡ Fetch Latest Events'}
            </button>

            {events.length > 0 && (
                <div style={{ marginBottom: '1.5rem', border: '1px solid #ddd' }}>
                    {events.map((e, i) => (
                        <div
                            key={i}
                            onClick={() => selectEvent(e)}
                            style={{
                                padding: '0.75rem 1rem',
                                borderBottom: i < events.length - 1 ? '1px solid #eee' : 'none',
                                cursor: 'pointer',
                                backgroundColor: 'white'
                            }}
                            onMouseEnter={ev => (ev.currentTarget.style.backgroundColor = '#f5f5f5')}
                            onMouseLeave={ev => (ev.currentTarget.style.backgroundColor = 'white')}
                        >
                            <div style={{ fontWeight: 500, marginBottom: '0.2rem' }}>{e.title}</div>
                            <div style={{ fontSize: '0.8rem', color: '#888' }}>{e.domain} · {e.seendate}</div>
                        </div>
                    ))}
                </div>
            )}

            <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.5rem' }}>
                    Topic / Event
                </label>
                <input
                    type="text"
                    value={topic}
                    onChange={e => setTopic(e.target.value)}
                    placeholder="e.g. Federal Reserve raises interest rates unexpectedly"
                    style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', boxSizing: 'border-box' }}
                />
            </div>

            <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.5rem' }}>
                    Additional Context (optional)
                </label>
                <textarea
                    value={context}
                    onChange={e => setContext(e.target.value)}
                    placeholder="e.g. Inflation data released same week, midterm elections approaching, banking sector under pressure..."
                    rows={4}
                    style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', boxSizing: 'border-box' }}
                />
            </div>

            <button
                onClick={analyze}
                disabled={loading || !topic.trim()}
                style={{
                    padding: '0.75rem 2rem',
                    fontSize: '1rem',
                    backgroundColor: loading ? '#999' : '#222',
                    color: 'white',
                    border: 'none',
                    cursor: loading ? 'not-allowed' : 'pointer'
                }}
            >
                {loading ? 'Analyzing...' : 'Follow the Incentives'}
            </button>

            {error && (
                <div style={{ marginTop: '1rem', padding: '1rem', backgroundColor: '#fee', color: '#c00' }}>
                    Error: {error}
                </div>
            )}

            {analysis && (
                <div style={{ marginTop: '2rem', padding: '1.5rem', backgroundColor: '#f5f5f5', whiteSpace: 'pre-wrap', lineHeight: '1.6' }}>
                    {analysis}
                </div>
            )}
        </div>
    )
}

export default function App() {
    return (
        <Authenticator hideSignUp={true}>
            {({ signOut, user }) => (
                <div>
                    <div style={{ textAlign: 'right', padding: '0.5rem 1rem', backgroundColor: '#f0f0f0', fontSize: '0.85rem' }}>
                        {user?.signInDetails?.loginId} &nbsp;
                        <button onClick={signOut} style={{ cursor: 'pointer' }}>Sign out</button>
                    </div>
                    <AnalyzerApp />
                </div>
            )}
        </Authenticator>
    )
}