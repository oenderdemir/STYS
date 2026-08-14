import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AgentInstallationSessionCreateRequest } from './agent-yonetimi.dto';
import { AgentYonetimiService } from './agent-yonetimi.service';

describe('AgentYonetimiService - installation session API', () => {
    let service: AgentYonetimiService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule]
        });

        service = TestBed.inject(AgentYonetimiService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('createInstallation kurulum requestinde kurumId gondermez ve required alanlari yollar', () => {
        const request: AgentInstallationSessionCreateRequest = {
            tesisId: 42,
            agentDisplayName: 'TRT Merkez Agent',
            targetRid: 'win-x64',
            scopes: ['agent.heartbeat', 'agent.command.read', 'agent.command.execute', 'agent.result.write', 'agent.config.read'],
            requiresApproval: true
        };

        service.createInstallation(request).subscribe();

        const req = httpMock.expectOne((x) => x.url.endsWith('/ui/agent-installations'));
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(jasmine.objectContaining({
            tesisId: 42,
            agentDisplayName: 'TRT Merkez Agent',
            targetRid: 'win-x64',
            requiresApproval: true,
            scopes: jasmine.arrayContaining(['agent.heartbeat', 'agent.command.execute'])
        }));
        expect(req.request.body.kurumId).toBeUndefined();

        req.flush({
            success: true,
            data: {
                session: {
                    id: 7,
                    kurumId: 1,
                    tesisId: 42,
                    tesisAd: 'TRT Merkez',
                    agentDisplayName: 'TRT Merkez Agent',
                    targetRid: 'win-x64',
                    scopes: request.scopes,
                    status: 3,
                    expiresAt: new Date().toISOString(),
                    createdAt: new Date().toISOString()
                },
                enrollmentCode: 'ABCD-EFGH'
            }
        });
    });
});
