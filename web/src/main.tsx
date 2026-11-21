import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import './index.css'
import AdminHome from './admin/AdminHome.tsx'
import AdminPage from './admin/AdminPage.tsx'
import HouseholdPage from './household/HouseholdPage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/admin" replace />} />
        <Route path="/admin" element={<AdminHome />} />
        <Route path="/admin/:runId" element={<AdminPage />} />
        <Route path="/admin/:runId/houses/:houseId" element={<HouseholdPage />} />
        <Route path="/household/*" element={<Navigate to="/admin" replace />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
