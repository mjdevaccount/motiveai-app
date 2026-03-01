import { useState } from 'react'
import { Authenticator } from '@aws-amplify/ui-react'
import { fetchAuthSession } from 'aws-amplify/auth'

const API_URL = import.meta.env.VITE_API_URL

function AnalyzerApp() {
    const [topic, setTopic] = useState('')
    const [context, setContext] = useState('')
    const [analysis, setAnalysis] = useState('')
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState('')

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
            <h1 style={{ borderBottom: '2px solid #333', paddingBottom: '0.5rem' }}>
                MotiveAI
            </h1>
            <p style={{ color: '#666', marginBottom: '2rem' }}>
                Follow the incentives. Ask not what happened — ask why.
            </p>

            <div style={{ marginBottom: '1rem' }}>
                <label style={{ display: 'block', fontWeight: 'bold', marginBottom: '0.5rem' }}>
                    Topic / Event
                </label>
                <input
                    type="text"
                    value={topic}
                    onChange={e => setTopic(e.target.value)}
                    placeholder="e.g. US strikes Iran on Friday night"
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
                    placeholder="e.g. Epstein files resurfacing, approval ratings at all-time low, election cycle timing..."
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